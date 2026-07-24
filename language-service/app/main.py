"""FastAPI service for writing/speaking feedback and exercise generation.
Backed by Claude, with a deterministic fallback when no API key is set."""
import logging

from dotenv import load_dotenv
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from . import claude_client, evaluator
from .agent import graph as coach_graph
from .rag import retriever, store
from .schemas import (
    CoachRequest,
    GenerateRequest,
    GenerateVocabRequest,
    RagIndexRequest,
    RagQueryRequest,
    SpeakingRequest,
    WritingRequest,
)

load_dotenv()
logging.basicConfig(level=logging.INFO)

app = FastAPI(title="MeineDeutscheLehrerin Language Service", version="1.0.0")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
def health():
    return {"status": "ok", "service": "language-service", "claude": claude_client.is_enabled()}


@app.post("/evaluate/writing")
def evaluate_writing(req: WritingRequest):
    return evaluator.evaluate_writing(req)


@app.post("/evaluate/speaking")
def evaluate_speaking(req: SpeakingRequest):
    return evaluator.evaluate_speaking(req)


@app.post("/generate/exercises")
def generate_exercises(req: GenerateRequest):
    return evaluator.generate_exercises(req)


@app.post("/generate/vocabulary")
def generate_vocabulary(req: GenerateVocabRequest):
    return evaluator.generate_vocabulary(req)


# ---------------- Grammar RAG ----------------


@app.post("/rag/index")
def rag_index(req: RagIndexRequest):
    """Rebuild the grammar index from curriculum documents pushed by the .NET API."""
    return retriever.index([d.model_dump() for d in req.docs])


@app.post("/rag/grammar")
def rag_grammar(req: RagQueryRequest):
    """Retrieve grammar explanations from the app's own content; optionally add a
    Claude answer grounded strictly in what was retrieved."""
    return retriever.answer(req.query, req.level, req.k, req.with_answer)


@app.get("/rag/stats")
def rag_stats():
    return store.get_store().stats()


# ---------------- Multi-agent Study Coach ----------------


@app.post("/coach/turn")
def coach_turn(req: CoachRequest):
    """Run one turn of the LangGraph multi-agent coach (planner + grammar/exercise/evaluator
    agents over the RAG, generation and grading tools), with per-thread memory."""
    return coach_graph.run_turn(
        req.user_id, req.message, req.level, req.goal, req.submission, req.thread_id)
