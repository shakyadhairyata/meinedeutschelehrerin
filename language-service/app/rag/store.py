"""Chunk storage and similarity search.

In production the index lives in a `rag_chunks` table in the existing Postgres via
pgvector — persistent across the free-tier restarts that would otherwise force a
re-embed on every boot. Without DATABASE_URL (local dev, CI) an in-memory store with
the same interface is used, so nothing about the calling code changes.
"""
import json
import logging
import os
from dataclasses import dataclass, field

from . import embeddings

logger = logging.getLogger("language-service")

TABLE = "rag_chunks"


@dataclass
class Chunk:
    id: str
    level: str
    source: str          # "lesson" | "exercise"
    title: str
    grammar_topic: str | None
    text: str
    embedding: list[float] = field(default_factory=list)


@dataclass
class Hit:
    chunk: Chunk
    score: float


def _database_url() -> str | None:
    url = os.getenv("RAG_DATABASE_URL") or os.getenv("DATABASE_URL")
    return url or None


# ---------------- in-memory ----------------

class MemoryStore:
    kind = "memory"

    def __init__(self) -> None:
        self._chunks: list[Chunk] = []
        self._model = ""
        self._idf: list[float] | None = None

    def replace_all(self, chunks: list[Chunk], model: str, idf: list[float] | None = None) -> int:
        self._chunks = chunks
        self._model = model
        self._idf = idf
        return len(chunks)

    def load_idf(self) -> list[float] | None:
        return self._idf

    def search(self, query_vec: list[float], level: str | None, k: int) -> list[Hit]:
        pool = [c for c in self._chunks if not level or c.level == level]
        scored = [Hit(c, embeddings.cosine(query_vec, c.embedding)) for c in pool]
        scored.sort(key=lambda h: h.score, reverse=True)
        return scored[:k]

    def stats(self) -> dict:
        return {"store": self.kind, "chunks": len(self._chunks), "model": self._model}


# ---------------- pgvector ----------------

class PgVectorStore:
    kind = "pgvector"

    def __init__(self, url: str) -> None:
        self._url = url

    def _connect(self):
        import psycopg

        return psycopg.connect(self._url)

    def _ensure_schema(self, cur, dim: int) -> None:
        cur.execute("CREATE EXTENSION IF NOT EXISTS vector")
        # Recreate the table if the embedding width changed (e.g. a different Voyage model),
        # since vectors from different models are not comparable.
        cur.execute(
            "SELECT a.atttypmod FROM pg_attribute a "
            "JOIN pg_class c ON c.oid = a.attrelid "
            "WHERE c.relname = %s AND a.attname = 'embedding'",
            (TABLE,),
        )
        row = cur.fetchone()
        if row and row[0] != dim:
            logger.info("rag: embedding width changed (%s -> %s), rebuilding table", row[0], dim)
            cur.execute(f"DROP TABLE IF EXISTS {TABLE}")
        cur.execute(
            f"CREATE TABLE IF NOT EXISTS {TABLE} ("
            " id text PRIMARY KEY,"
            " level text NOT NULL,"
            " source text NOT NULL,"
            " title text NOT NULL,"
            " grammar_topic text,"
            " text text NOT NULL,"
            " model text NOT NULL,"
            f" embedding vector({dim}) NOT NULL)"
        )
        cur.execute(f"CREATE INDEX IF NOT EXISTS {TABLE}_level_idx ON {TABLE} (level)")
        # IDF weights belong to the corpus, not to any one chunk, and queries must be embedded
        # with the same weights that built the index — so they are persisted alongside it.
        cur.execute(f"CREATE TABLE IF NOT EXISTS {TABLE}_meta (key text PRIMARY KEY, value text NOT NULL)")

    def replace_all(self, chunks: list[Chunk], model: str, idf: list[float] | None = None) -> int:
        if not chunks:
            return 0
        dim = len(chunks[0].embedding)
        with self._connect() as conn, conn.cursor() as cur:
            self._ensure_schema(cur, dim)
            cur.execute(f"DELETE FROM {TABLE}")
            cur.execute(
                f"INSERT INTO {TABLE}_meta (key, value) VALUES ('idf', %s)"
                " ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value",
                (json.dumps(idf) if idf else "null",),
            )
            cur.executemany(
                f"INSERT INTO {TABLE} (id, level, source, title, grammar_topic, text, model, embedding)"
                " VALUES (%s, %s, %s, %s, %s, %s, %s, %s)",
                [
                    (c.id, c.level, c.source, c.title, c.grammar_topic, c.text, model,
                     "[" + ",".join(f"{v:.6f}" for v in c.embedding) + "]")
                    for c in chunks
                ],
            )
            conn.commit()
        return len(chunks)

    def search(self, query_vec: list[float], level: str | None, k: int) -> list[Hit]:
        vec = "[" + ",".join(f"{v:.6f}" for v in query_vec) + "]"
        sql = (
            f"SELECT id, level, source, title, grammar_topic, text, 1 - (embedding <=> %s::vector) AS score"
            f" FROM {TABLE}"
        )
        params: list = [vec]
        if level:
            sql += " WHERE level = %s"
            params.append(level)
        sql += " ORDER BY embedding <=> %s::vector LIMIT %s"
        params += [vec, k]
        with self._connect() as conn, conn.cursor() as cur:
            cur.execute(sql, params)
            return [
                Hit(Chunk(id=r[0], level=r[1], source=r[2], title=r[3], grammar_topic=r[4], text=r[5]),
                    float(r[6]))
                for r in cur.fetchall()
            ]

    def load_idf(self) -> list[float] | None:
        try:
            with self._connect() as conn, conn.cursor() as cur:
                cur.execute(f"SELECT value FROM {TABLE}_meta WHERE key = 'idf'")
                row = cur.fetchone()
                return json.loads(row[0]) if row else None
        except Exception:  # noqa: BLE001 — not indexed yet
            return None

    def stats(self) -> dict:
        try:
            with self._connect() as conn, conn.cursor() as cur:
                cur.execute(f"SELECT count(*), coalesce(max(model), '') FROM {TABLE}")
                n, model = cur.fetchone()
                return {"store": self.kind, "chunks": int(n), "model": model}
        except Exception as exc:  # noqa: BLE001 — table may not exist yet
            return {"store": self.kind, "chunks": 0, "model": "", "error": str(exc)}


# ---------------- selection ----------------

_store = None


def get_store():
    """pgvector when a database is configured and reachable, else in-memory."""
    global _store
    if _store is not None:
        return _store
    url = _database_url()
    if url:
        try:
            import psycopg  # noqa: F401 — probe the driver before committing to pgvector

            store = PgVectorStore(url)
            with store._connect():
                pass
            _store = store
            logger.info("rag: using pgvector store")
            return _store
        except Exception as exc:  # noqa: BLE001
            logger.warning("rag: pgvector unavailable (%s), using in-memory store", exc)
    _store = MemoryStore()
    return _store


def reset_store_for_tests() -> None:
    global _store
    _store = None
