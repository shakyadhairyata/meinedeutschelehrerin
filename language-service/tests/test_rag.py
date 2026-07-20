"""Tests for grammar retrieval. These run with no VOYAGE_API_KEY and no database:
the deterministic hashing embedder and the in-memory store keep CI keyless."""
import pytest

from app.rag import retriever, store

DOCS = [
    {
        "level": "A1", "source": "lesson", "title": "Der Akkusativ", "grammarTopic": "Akkusativ",
        "text": "Der Akkusativ ist der Fall des direkten Objekts. Nur der maskuline Artikel ändert "
                "sich: der wird zu den. Beispiel: Ich sehe den Mann.\n\n"
                "Nach den Verben haben, sehen und kaufen steht immer der Akkusativ.",
    },
    {
        "level": "A1", "source": "lesson", "title": "Die Modalverben", "grammarTopic": "Modalverben",
        "text": "Modalverben wie können, müssen und wollen stehen an Position zwei. "
                "Das zweite Verb steht im Infinitiv am Satzende. Beispiel: Ich muss heute arbeiten.",
    },
    {
        "level": "B1", "source": "lesson", "title": "Wechselpräpositionen", "grammarTopic": "Wechselpräpositionen",
        "text": "Wechselpräpositionen wie in, an und auf stehen mit Akkusativ bei Bewegung "
                "und mit Dativ bei Ort. Wohin? Akkusativ. Wo? Dativ.",
    },
    {
        "level": "B1", "source": "exercise", "title": "Konjunktiv II", "grammarTopic": "Konjunktiv II",
        "text": "Mit dem Konjunktiv II drückt man Wünsche und höfliche Bitten aus. "
                "Beispiel: Ich hätte gern einen Kaffee. Könnten Sie mir bitte helfen?",
    },
    {
        # Deliberately at B1 while the regression test queries as an A2 learner.
        "level": "B1", "source": "lesson", "title": "Nebensätze und Konnektoren", "grammarTopic": "Nebensatz",
        "text": "In Nebensätzen steht das konjugierte Verb am Ende. Mit weil gibt man einen Grund an: "
                "Ich lerne Deutsch, weil ich in Berlin arbeiten möchte. Auch nach dass und wenn "
                "steht das konjugierte Verb am Satzende.",
    },
]


@pytest.fixture(autouse=True)
def fresh_store():
    store.reset_store_for_tests()
    yield
    store.reset_store_for_tests()


def test_index_reports_chunk_count_and_model():
    res = retriever.index(DOCS)
    assert res["indexed"] >= len(DOCS)
    assert res["store"] == "memory"
    assert res["model"].startswith("hashing-")


def test_search_finds_the_matching_grammar_topic():
    retriever.index(DOCS)
    hits = retriever.search("Wann benutzt man den Akkusativ?", k=3)
    assert hits, "expected at least one hit"
    assert hits[0].chunk.grammar_topic == "Akkusativ"
    assert hits[0].score > 0


def test_search_distinguishes_between_topics():
    retriever.index(DOCS)
    modal = retriever.search("Wo steht das zweite Verb bei Modalverben?", k=1)
    konj = retriever.search("Wie formuliere ich eine höfliche Bitte?", k=1)
    assert modal[0].chunk.grammar_topic == "Modalverben"
    assert konj[0].chunk.grammar_topic == "Konjunktiv II"


def test_search_filters_by_level():
    retriever.index(DOCS)
    hits = retriever.search("Wechselpräpositionen mit Dativ", level="B1", k=5)
    assert hits
    assert all(h.chunk.level == "B1" for h in hits)


def test_search_falls_back_across_levels_when_level_has_no_content():
    retriever.index(DOCS)
    hits = retriever.search("Akkusativ", level="C1", k=3)
    assert hits, "an unindexed level should still return the best available explanation"


def test_level_is_a_preference_not_a_filter():
    """Regression: a hard level filter returned unrelated same-level material instead of the
    lesson that actually answers the question. Nebensätze is taught at B1, so an A2 learner
    asking about verb-final word order must still reach it."""
    retriever.index(DOCS)
    hits = retriever.search("Warum steht das Verb am Ende im Nebensatz?", level="A2", k=3)
    assert hits, "expected a hit from another level"
    assert hits[0].chunk.level == "B1"
    assert "Wechselpräpositionen" not in hits[0].chunk.title


def test_same_level_wins_when_relevance_is_comparable():
    retriever.index(DOCS)
    a1 = retriever.search("Akkusativ", level="A1", k=1)
    assert a1[0].chunk.level == "A1"


def test_lesson_explanations_outrank_exercise_rationales():
    docs = [
        {"level": "A2", "source": "exercise", "title": "Perfekt", "grammarTopic": "Perfekt",
         "text": "Ergänze: Ich ___ gestern Fußball gespielt. Lösung: habe."},
        {"level": "A2", "source": "lesson", "title": "Perfekt", "grammarTopic": "Perfekt",
         "text": "Das Perfekt bildet man mit haben oder sein plus Partizip II. "
                 "Beispiel: Ich habe gestern Fußball gespielt."},
    ]
    retriever.index(docs)
    hits = retriever.search("Wie bildet man das Perfekt?", level="A2", k=2)
    assert hits[0].chunk.source == "lesson"


def test_irrelevant_query_returns_no_sources_rather_than_noise():
    retriever.index(DOCS)
    assert retriever.search("Bundesliga Ergebnisse vom Wochenende", k=3) == []


def test_empty_query_returns_nothing():
    retriever.index(DOCS)
    assert retriever.search("   ") == []


def test_answer_returns_sources_without_calling_claude():
    retriever.index(DOCS)
    res = retriever.answer("Was ist der Akkusativ?", level="A1", with_answer=False)
    assert res["answer"] is None
    assert res["sources"], "retrieval should always return sources"
    assert res["sources"][0]["grammarTopic"] == "Akkusativ"
    assert 0 <= res["sources"][0]["score"] <= 1


def test_answer_is_null_without_api_key_but_sources_are_returned():
    retriever.index(DOCS)
    res = retriever.answer("Was ist der Akkusativ?", level="A1", with_answer=True)
    # No ANTHROPIC_API_KEY in CI: no generated summary, but the retrieved explanation is present
    # in sources (not duplicated into a fabricated "answer").
    assert res["answer"] is None
    assert res["grounded"] is False
    assert any("Akkusativ" in s["text"] for s in res["sources"])


def test_long_text_is_split_into_multiple_chunks():
    long_doc = [{
        "level": "A2", "source": "lesson", "title": "Langer Text", "grammarTopic": "Perfekt",
        "text": "\n\n".join(f"Absatz {i}: " + "Das Perfekt bildet man mit haben oder sein. " * 6
                            for i in range(4)),
    }]
    chunks = retriever.build_chunks(long_doc)
    assert len(chunks) > 1
    assert all(len(c.text) <= retriever.MAX_CHARS + len(c.title) + 40 for c in chunks)


def test_chunks_are_deduplicated():
    doubled = DOCS + DOCS
    assert len(retriever.build_chunks(doubled)) == len(retriever.build_chunks(DOCS))


def test_chunk_text_includes_title_and_topic_for_embedding():
    chunks = retriever.build_chunks(DOCS[:1])
    assert chunks[0].text.startswith("Der Akkusativ (Akkusativ)")
