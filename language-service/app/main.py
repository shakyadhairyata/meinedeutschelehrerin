"""FastAPI service for writing/speaking feedback and exercise generation.
Backed by Claude, with a deterministic fallback when no API key is set."""
import logging

from dotenv import load_dotenv
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from . import claude_client, evaluator
from .schemas import GenerateRequest, GenerateVocabRequest, SpeakingRequest, WritingRequest

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
