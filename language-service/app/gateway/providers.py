"""Provider abstraction.

Each provider wraps a LangChain chat model behind one interface, so the router can treat
Anthropic, OpenAI and an open-source model (Ollama) identically and fall back between them. A
provider reports whether it's `available()` (its key/host is configured) without constructing a
client, so the router can skip unconfigured providers cheaply. `complete()` returns the text plus
the token usage and latency the router needs for cost accounting.
"""
import os
import time
from dataclasses import dataclass


@dataclass
class ProviderResult:
    text: str
    provider: str
    model: str
    input_tokens: int
    output_tokens: int
    latency_ms: float


# Approximate list prices in USD per 1M tokens (input, output). Matched by substring; open-source
# via Ollama is self-hosted, so zero marginal cost. Update as pricing changes.
PRICES: dict[str, tuple[float, float]] = {
    "claude-opus": (15.0, 75.0),
    "claude-sonnet": (3.0, 15.0),
    "claude-haiku": (0.80, 4.0),
    "gpt-4o-mini": (0.15, 0.60),
    "gpt-4o": (2.50, 10.0),
    "gpt-4.1-mini": (0.40, 1.60),
    "llama": (0.0, 0.0),
    "mistral": (0.0, 0.0),
    "qwen": (0.0, 0.0),
}


def estimate_cost(model: str, input_tokens: int, output_tokens: int) -> float:
    for prefix, (pin, pout) in PRICES.items():
        if prefix in model:
            return round(input_tokens / 1_000_000 * pin + output_tokens / 1_000_000 * pout, 6)
    return 0.0


class LangChainProvider:
    """Base for a provider backed by a LangChain chat model."""
    name = "base"

    def __init__(self, model: str):
        self.model = model

    def available(self) -> bool:  # pragma: no cover - overridden
        raise NotImplementedError

    def _build(self, temperature: float, max_tokens: int):  # pragma: no cover - overridden
        raise NotImplementedError

    def complete(self, prompt: str, *, system: str | None = None, temperature: float, max_tokens: int,
                 config: dict | None = None) -> ProviderResult:
        from langchain_core.messages import HumanMessage, SystemMessage

        client = self._build(temperature, max_tokens)
        messages = ([SystemMessage(content=system)] if system else []) + [HumanMessage(content=prompt)]
        t0 = time.perf_counter()
        resp = client.invoke(messages, config=config or {})
        latency = (time.perf_counter() - t0) * 1000
        usage = getattr(resp, "usage_metadata", None) or {}
        content = resp.content if isinstance(resp.content, str) else str(resp.content)
        return ProviderResult(
            text=(content or "").strip(),
            provider=self.name, model=self.model,
            input_tokens=int(usage.get("input_tokens", 0) or 0),
            output_tokens=int(usage.get("output_tokens", 0) or 0),
            latency_ms=round(latency, 1),
        )


class AnthropicProvider(LangChainProvider):
    name = "anthropic"

    def __init__(self, model: str | None = None):
        super().__init__(model or os.getenv("ANTHROPIC_MODEL") or os.getenv("LLM_MODEL", "claude-sonnet-4-6"))

    def available(self) -> bool:
        return bool(os.getenv("ANTHROPIC_API_KEY"))

    def _build(self, temperature, max_tokens):
        from langchain_anthropic import ChatAnthropic

        return ChatAnthropic(model=self.model, temperature=temperature, max_tokens=max_tokens)


class OpenAIProvider(LangChainProvider):
    name = "openai"

    def __init__(self, model: str | None = None):
        super().__init__(model or os.getenv("OPENAI_MODEL", "gpt-4o-mini"))

    def available(self) -> bool:
        return bool(os.getenv("OPENAI_API_KEY"))

    def _build(self, temperature, max_tokens):
        from langchain_openai import ChatOpenAI

        # base_url lets this also point at an OpenAI-compatible open-source endpoint (Together, Groq…).
        return ChatOpenAI(model=self.model, temperature=temperature, max_tokens=max_tokens,
                          base_url=os.getenv("OPENAI_BASE_URL") or None)


class OllamaProvider(LangChainProvider):
    name = "ollama"

    def __init__(self, model: str | None = None):
        super().__init__(model or os.getenv("OLLAMA_MODEL", "llama3.1"))

    def available(self) -> bool:
        return bool(os.getenv("OLLAMA_BASE_URL") or os.getenv("OLLAMA_HOST"))

    def _build(self, temperature, max_tokens):
        from langchain_ollama import ChatOllama

        return ChatOllama(model=self.model, temperature=temperature, num_predict=max_tokens,
                          base_url=os.getenv("OLLAMA_BASE_URL") or os.getenv("OLLAMA_HOST"))


CATALOG = {
    "anthropic": AnthropicProvider,
    "openai": OpenAIProvider,
    "ollama": OllamaProvider,
}
