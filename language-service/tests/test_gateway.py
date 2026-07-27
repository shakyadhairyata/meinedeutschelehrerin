"""Tests for the multi-provider LLM gateway: fallback routing and cost/latency accounting.

Fake providers stand in for Anthropic/OpenAI/Ollama so the routing logic is tested with no keys
and no network."""
import pytest

from app.agent import observability as obs
from app.gateway.providers import ProviderResult, estimate_cost
from app.gateway.router import AllProvidersFailed, LLMGateway, default_providers


class FakeProvider:
    def __init__(self, name, *, model=None, available=True, fail=False, tokens=(10, 5)):
        self.name = name
        self.model = model or f"{name}-model"
        self._available = available
        self._fail = fail
        self._tokens = tokens
        self.calls = 0

    def available(self):
        return self._available

    def complete(self, prompt, *, temperature, max_tokens, config=None):
        self.calls += 1
        if self._fail:
            raise RuntimeError("provider unavailable")
        return ProviderResult("ok", self.name, self.model, self._tokens[0], self._tokens[1], 1.0)


def test_primary_serves_when_available():
    p1, p2 = FakeProvider("anthropic"), FakeProvider("openai")
    obs.start_run()
    r = LLMGateway([p1, p2]).complete("hi")
    assert r.provider == "anthropic"
    assert p2.calls == 0  # secondary never touched


def test_falls_back_when_primary_fails():
    p1, p2 = FakeProvider("anthropic", fail=True), FakeProvider("openai")
    obs.start_run()
    r = LLMGateway([p1, p2]).complete("hi")
    assert r.provider == "openai"
    assert p1.calls == 1 and p2.calls == 1
    m = obs.metrics_dict()
    assert any(e["provider"] == "anthropic" for e in m["fallbacks"])
    assert any(p.startswith("openai:") for p in m["providers"])


def test_skips_unavailable_without_calling_it():
    p1, p2 = FakeProvider("anthropic", available=False), FakeProvider("openai")
    obs.start_run()
    r = LLMGateway([p1, p2]).complete("hi")
    assert r.provider == "openai"
    assert p1.calls == 0  # unavailable provider is not invoked


def test_all_failing_raises():
    obs.start_run()
    gw = LLMGateway([FakeProvider("a", fail=True), FakeProvider("b", fail=True)])
    with pytest.raises(AllProvidersFailed):
        gw.complete("hi")


def test_none_configured_raises():
    obs.start_run()
    with pytest.raises(AllProvidersFailed):
        LLMGateway([FakeProvider("a", available=False)]).complete("hi")


def test_cost_and_tokens_are_recorded():
    obs.start_run()
    LLMGateway([FakeProvider("openai", model="gpt-4o-mini", tokens=(1000, 1000))]).complete("hi")
    m = obs.metrics_dict()
    assert m["totalTokens"] == 2000
    # gpt-4o-mini: 0.15 in + 0.60 out per 1M tokens
    assert m["costUsd"] == pytest.approx(0.00075, rel=1e-3)


def test_estimate_cost_matches_by_model_substring():
    assert estimate_cost("claude-sonnet-4-6", 1_000_000, 1_000_000) == pytest.approx(18.0)
    assert estimate_cost("gpt-4o-mini", 1_000_000, 0) == pytest.approx(0.15)
    assert estimate_cost("llama3.1", 1_000_000, 1_000_000) == 0.0     # self-hosted
    assert estimate_cost("unknown-model", 1_000_000, 1_000_000) == 0.0  # unknown → 0


def test_default_provider_order_from_env(monkeypatch):
    monkeypatch.setenv("LLM_PROVIDERS", "openai,anthropic")
    names = [p.name for p in default_providers()]
    assert names == ["openai", "anthropic"]


def test_describe_reports_availability():
    gw = LLMGateway([FakeProvider("anthropic", available=True), FakeProvider("ollama", available=False)])
    desc = {d["name"]: d["available"] for d in gw.describe()}
    assert desc == {"anthropic": True, "ollama": False}
