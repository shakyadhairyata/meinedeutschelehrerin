"""Tools the coaching agents invoke.

Each tool is a thin, side-effect-free wrapper over a capability the product already has —
grammar retrieval (RAG), exercise generation, and grading — exposed as a plain callable plus a
LangChain `@tool` so an LLM-driven agent can also call it by name. Keeping the logic here (not in
the agents) means the agents stay about *coordination*, and every tool has a deterministic
offline path, so the whole graph runs without an API key.
"""
from typing import Any, Optional

from langchain_core.tools import tool

from .. import evaluator
from ..rag import retriever
from ..schemas import GenerateRequest, WritingRequest

VALID_SKILLS = {"Grammar", "Vocabulary", "Reading", "Listening", "Speaking", "Writing"}


# ---------------- plain callables (used directly by nodes; always available) ----------------

def grammar_lookup(query: str, level: str | None = None, with_answer: bool = False) -> dict:
    """Retrieve grammar explanations from the app's own lessons (RAG). Free; no tokens unless
    with_answer asks for a grounded summary."""
    return retriever.answer(query, level, k=3, with_answer=with_answer)


def make_exercise(topic: str, level: str, skill: str = "Grammar",
                  grammar_topic: str | None = None) -> Optional[dict]:
    """Generate a single practice exercise targeted at a topic/skill for a level."""
    skill = skill if skill in VALID_SKILLS else "Grammar"
    res = evaluator.generate_exercises(GenerateRequest(
        level=level or "A1", skill=skill, topic=topic or "",
        grammar_topic=grammar_topic, count=1))
    items = res.get("exercises") or []
    return items[0] if items else None


def grade_answer(exercise: dict, submission: dict, level: str | None = None) -> dict:
    """Grade a learner's answer to an exercise. Auto-grades multiple-choice by index; routes
    free-text answers through the writing evaluator (Claude, with an offline fallback)."""
    exercise = exercise or {}
    submission = submission or {}
    solution = exercise.get("solution") or {}

    if "correctIndex" in solution and submission.get("selectedIndex") is not None:
        correct = solution["correctIndex"]
        ok = correct == submission["selectedIndex"]
        return {
            "isCorrect": ok,
            "scorePercent": 100.0 if ok else 0.0,
            "explanation": exercise.get("explanation", ""),
            "correctIndex": correct,
        }

    text = submission.get("text", "") or ""
    fb = evaluator.evaluate_writing(WritingRequest(
        prompt=exercise.get("prompt", ""), text=text, level=level or "A1", min_words=0))
    return {
        "isCorrect": fb.get("scorePercent", 0) >= 60,
        "scorePercent": fb.get("scorePercent", 0),
        "explanation": fb.get("summary", ""),
        "feedback": fb,
    }


# ---------------- LangChain tools (for LLM-driven tool calling; same logic) ----------------

@tool
def grammar_lookup_tool(query: str, level: str = "") -> dict:
    """Look up how a German grammar point works, using the course's own lessons.
    Use for any 'why/when/how' grammar question. `level` is the CEFR level (A1..C1) or empty."""
    return grammar_lookup(query, level or None, with_answer=False)


@tool
def make_exercise_tool(topic: str, level: str, skill: str = "Grammar") -> dict | None:
    """Create one practice exercise on a topic for a CEFR level and skill
    (Grammar/Reading/Listening/Writing/Speaking/Vocabulary)."""
    return make_exercise(topic, level, skill)


TOOLS = [grammar_lookup_tool, make_exercise_tool]
TOOLS_BY_NAME: dict[str, Any] = {t.name: t for t in TOOLS}
