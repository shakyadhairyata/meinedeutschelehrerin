"""LLM access for the agents.

Uses LangChain's ChatAnthropic when ANTHROPIC_API_KEY is set — so calls flow through the same
framework LangGraph runs on and are picked up by LangSmith tracing — and degrades to deterministic
heuristics when there is no key, so the graph (and its tests) run offline. Every model call records
token usage and its prompt version through the observability layer.
"""
import logging
import os
import re

from . import observability as obs
from .prompts import get as get_prompt

logger = logging.getLogger("language-service")

_QUESTION_WORDS = ("warum", "wieso", "weshalb", "wie", "wann", "was", "welche", "wo")
_PRACTICE_WORDS = ("üben", "übung", "practice", "practise", "aufgabe", "exercise", "quiz", "test mich")


def chat_enabled() -> bool:
    return bool(os.getenv("ANTHROPIC_API_KEY"))


def _model(temperature: float = 0.0, max_tokens: int = 512):
    from langchain_anthropic import ChatAnthropic

    return ChatAnthropic(
        model=os.getenv("LLM_MODEL", "claude-sonnet-4-6"),
        temperature=temperature,
        max_tokens=max_tokens,
    )


def _invoke(prompt_name: str, rendered: str, *, temperature: float, max_tokens: int) -> str:
    """Invoke the model, stamping the call with its prompt version and recording usage."""
    prompt = get_prompt(prompt_name)
    resp = _model(temperature=temperature, max_tokens=max_tokens).invoke(
        rendered, config=obs.langchain_config(prompt))
    obs.record_llm(getattr(resp, "usage_metadata", None), prompt)
    return (resp.content or "").strip()


def classify_intent(message: str, has_submission: bool) -> str:
    """Return one of: 'evaluate' | 'grammar' | 'practice' | 'chat'.

    A submitted answer always means grading. Otherwise a heuristic reads the message; with a key
    the model refines ambiguous cases, but the heuristic alone is enough for the graph to work.
    """
    if has_submission:
        return "evaluate"
    text = (message or "").lower()
    if any(w in text for w in _PRACTICE_WORDS):
        return "practice"
    if "?" in text or any(text.startswith(w) or f" {w} " in text for w in _QUESTION_WORDS):
        return "grammar"

    if not chat_enabled():
        # No key: default to grammar help, the most useful fallback for a bare statement.
        return "grammar" if text.strip() else "chat"
    try:
        rendered = get_prompt("coach.intent").render(message=message)
        label = re.sub(r"[^a-z]", "", _invoke("coach.intent", rendered, temperature=0, max_tokens=8).lower())
        return label if label in {"grammar", "practice", "chat"} else "chat"
    except Exception as exc:  # noqa: BLE001
        logger.warning("intent classification failed, defaulting to chat: %s", exc)
        return "chat"


def summarize_reply(kind: str, context: str, level: str) -> str | None:
    """Compose a short, encouraging coach message around an agent's result. Returns None with no
    key so the caller uses its own templated reply."""
    if not chat_enabled():
        return None
    try:
        rendered = get_prompt("coach.reply").render(kind=kind, context=context, level=level)
        return _invoke("coach.reply", rendered, temperature=0.3, max_tokens=220)
    except Exception as exc:  # noqa: BLE001
        logger.warning("reply composition failed, using template: %s", exc)
        return None
