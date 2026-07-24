"""Lightweight LLMOps instrumentation for the coach.

Two layers:
- **Tracing** — LangGraph/LangChain emit full run traces to LangSmith automatically when
  `LANGSMITH_TRACING=true` and `LANGSMITH_API_KEY` are set; there is nothing to wire beyond
  the environment, and `tracing_enabled()` just reports whether it's on.
- **Per-turn metrics** — every turn collects latency, LLM call count, token usage and the prompt
  versions used, so cost/latency are visible even without an external service. A contextvar keeps
  this correct under concurrent requests.

`langchain_config(prompt)` stamps each model call with its prompt version as a LangSmith tag +
metadata, so traces can be grouped and compared by prompt version.
"""
import contextvars
import os
import time
from dataclasses import dataclass, field

from .prompts import Prompt

_run: contextvars.ContextVar["RunMetrics | None"] = contextvars.ContextVar("coach_run", default=None)


def tracing_enabled() -> bool:
    return os.getenv("LANGSMITH_TRACING", "").lower() in ("1", "true", "yes") and bool(
        os.getenv("LANGSMITH_API_KEY"))


@dataclass
class RunMetrics:
    started: float = field(default_factory=time.perf_counter)
    llm_calls: int = 0
    input_tokens: int = 0
    output_tokens: int = 0
    prompt_versions: list[str] = field(default_factory=list)

    def as_dict(self) -> dict:
        return {
            "latencyMs": round((time.perf_counter() - self.started) * 1000, 1),
            "llmCalls": self.llm_calls,
            "inputTokens": self.input_tokens,
            "outputTokens": self.output_tokens,
            "totalTokens": self.input_tokens + self.output_tokens,
            "promptVersions": self.prompt_versions,
            "tracing": tracing_enabled(),
        }


def start_run() -> None:
    _run.set(RunMetrics())


def current() -> "RunMetrics | None":
    return _run.get()


def record_llm(usage_metadata: dict | None, prompt: Prompt) -> None:
    """Called by the LLM helper after each model invocation."""
    m = _run.get()
    if m is None:
        return
    m.llm_calls += 1
    tag = f"{prompt.name}@{prompt.version}"
    if tag not in m.prompt_versions:
        m.prompt_versions.append(tag)
    usage = usage_metadata or {}
    m.input_tokens += int(usage.get("input_tokens", 0) or 0)
    m.output_tokens += int(usage.get("output_tokens", 0) or 0)


def metrics_dict() -> dict:
    m = _run.get()
    return m.as_dict() if m else {}


def langchain_config(prompt: Prompt) -> dict:
    """Config passed to a LangChain model call so the trace carries the prompt version."""
    return {
        "run_name": prompt.name,
        "tags": [f"prompt:{prompt.name}@{prompt.version}"],
        "metadata": {"prompt_name": prompt.name, "prompt_version": prompt.version},
    }
