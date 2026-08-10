"""The gateway router: try providers in order, fall back on failure, account for every call.

The provider order comes from `LLM_PROVIDERS` (default `anthropic,openai,ollama`). The router
skips providers that aren't configured, tries the rest in order, and on any error — rate limit,
timeout, outage — falls back to the next. Each successful call is recorded with its provider,
token usage, latency and estimated cost; each failure is recorded too, so a turn's metrics show
what was tried and what it cost. If every provider is unavailable or fails, it raises
`AllProvidersFailed`, and the agents fall back to their deterministic paths.
"""
import json
import logging
import os

from ..agent import observability as obs
from .providers import CATALOG, ProviderResult, estimate_cost

logger = logging.getLogger("language-service")

DEFAULT_ORDER = "anthropic,openai,ollama"


class AllProvidersFailed(RuntimeError):
    pass


class LLMGateway:
    def __init__(self, providers: list):
        self.providers = providers

    def complete(self, prompt: str, *, system: str | None = None, temperature: float = 0.0,
                 max_tokens: int = 512, config: dict | None = None) -> ProviderResult:
        errors: list[str] = []
        for provider in self.providers:
            try:
                if not provider.available():
                    continue
            except Exception:  # noqa: BLE001 — a broken availability check shouldn't stop routing
                continue
            try:
                result = provider.complete(prompt, system=system, temperature=temperature,
                                           max_tokens=max_tokens, config=config)
                cost = estimate_cost(result.model, result.input_tokens, result.output_tokens)
                obs.record_provider_call(result, cost)
                if errors:
                    logger.info("gateway: served by %s after %d fallback(s)", provider.name, len(errors))
                return result
            except Exception as exc:  # noqa: BLE001 — fall back to the next provider
                logger.warning("gateway: provider %s failed (%s); falling back", provider.name, exc)
                obs.record_provider_error(provider.name, str(exc))
                errors.append(f"{provider.name}: {exc}")
        raise AllProvidersFailed(f"no provider succeeded: {errors or 'none configured'}")

    def complete_json(self, system: str, user: str, *, temperature: float = 0.0,
                      max_tokens: int = 1024, config: dict | None = None) -> dict | None:
        """A JSON-returning call routed with the same fallback + cost/latency accounting as
        complete(). Returns None when every provider is unavailable/failing, or when the reply
        isn't a JSON object — so callers fall back to their deterministic path, exactly as they
        did with the old direct client."""
        try:
            result = self.complete(user, system=system, temperature=temperature,
                                   max_tokens=max_tokens, config=config)
        except AllProvidersFailed:
            return None
        return _parse_json(result.text)

    def describe(self) -> list[dict]:
        out = []
        for p in self.providers:
            try:
                ok = p.available()
            except Exception:  # noqa: BLE001
                ok = False
            out.append({"name": p.name, "model": p.model, "available": ok})
        return out


def default_providers() -> list:
    order = [s.strip() for s in os.getenv("LLM_PROVIDERS", DEFAULT_ORDER).split(",") if s.strip()]
    return [CATALOG[name]() for name in order if name in CATALOG]


_gateway: LLMGateway | None = None


def get_gateway() -> LLMGateway:
    global _gateway
    if _gateway is None:
        _gateway = LLMGateway(default_providers())
    return _gateway


def set_gateway_for_tests(gateway: LLMGateway | None) -> None:
    global _gateway
    _gateway = gateway


def any_available() -> bool:
    return any(d["available"] for d in get_gateway().describe())


def _parse_json(text: str) -> dict | None:
    """Parse a JSON object from a model reply, tolerating ```json fences. None if not an object."""
    t = (text or "").strip()
    if t.startswith("```"):
        t = t.split("```", 2)[1] if t.count("```") >= 2 else t
        if t.lstrip().lower().startswith("json"):
            t = t.lstrip()[4:]
        t = t.strip()
    try:
        obj = json.loads(t)
    except Exception:  # noqa: BLE001 — a non-JSON reply means fall back
        return None
    return obj if isinstance(obj, dict) else None
