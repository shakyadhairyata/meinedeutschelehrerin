"""Thin Claude wrapper. Returns parsed JSON, or None on any failure so callers can
fall back to a heuristic. Without ANTHROPIC_API_KEY the service runs in offline mode."""
import json
import logging
import os

logger = logging.getLogger("language-service")

DEFAULT_MODEL = os.getenv("LLM_MODEL", "claude-sonnet-4-6")


def is_enabled() -> bool:
    return bool(os.getenv("ANTHROPIC_API_KEY"))


def _strip_fences(text: str) -> str:
    text = text.strip()
    if text.startswith("```"):
        # remove ```json ... ``` fences
        text = text.split("```", 2)[1] if text.count("```") >= 2 else text
        if text.lstrip().lower().startswith("json"):
            text = text.lstrip()[4:]
    return text.strip()


def call_json(system: str, user: str, max_tokens: int = 1600) -> dict | None:
    """Call Claude and parse a JSON object from the reply. None on any error."""
    if not is_enabled():
        return None
    try:
        from anthropic import Anthropic

        client = Anthropic()  # reads ANTHROPIC_API_KEY from env
        msg = client.messages.create(
            model=DEFAULT_MODEL,
            max_tokens=max_tokens,
            system=system,
            messages=[{"role": "user", "content": user}],
        )
        raw = "".join(block.text for block in msg.content if block.type == "text")
        return json.loads(_strip_fences(raw))
    except Exception as exc:  # noqa: BLE001 - fall back to the offline path on any error
        logger.warning("Claude call failed, using offline fallback: %s", exc)
        return None
