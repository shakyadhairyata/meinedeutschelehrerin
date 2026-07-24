"""Tests for the multi-agent Study Coach graph.

They run with no ANTHROPIC_API_KEY and no database: an in-memory checkpointer and the offline
paths of every tool keep CI keyless. The point is to prove the orchestration — routing, tool
use, and cross-turn memory — not the quality of a live model's prose.
"""
import pytest
from langgraph.checkpoint.memory import MemorySaver

from app.agent import graph, llm
from app.agent import observability as obs
from app.rag import retriever, store

CORPUS = [
    {"level": "A2", "source": "lesson", "title": "Nebensätze mit weil", "grammarTopic": "Nebensatz",
     "text": "Im Nebensatz mit weil steht das konjugierte Verb am Ende: "
             "Ich bleibe zu Hause, weil ich krank bin."},
    {"level": "A1", "source": "lesson", "title": "Der Akkusativ", "grammarTopic": "Akkusativ",
     "text": "Im Akkusativ wird nur der maskuline Artikel verändert: der → den, ein → einen."},
]


@pytest.fixture(autouse=True)
def fresh_graph():
    store.reset_store_for_tests()
    retriever.index(CORPUS)
    graph.reset_for_tests(checkpointer=MemorySaver())  # explicit: never touch a database in tests
    yield
    store.reset_store_for_tests()


def agents(result) -> list[str]:
    return [s["agent"] for s in result["steps"]]


def test_grammar_turn_runs_planner_explainer_and_generator():
    r = graph.run_turn("u", "Warum steht das Verb am Ende nach weil?", level="A2", thread_id="t")
    assert r["plan"] == ["grammar", "exercise"]
    assert agents(r) == ["planner", "grammar_explainer", "exercise_generator"]


def test_grammar_agent_invokes_the_rag_tool():
    r = graph.run_turn("u", "Wann benutzt man den Akkusativ?", level="A1", thread_id="t")
    g = r["grammar"]
    assert g and g["sources"], "grammar agent should return retrieved sources"
    assert any(s["grammarTopic"] == "Akkusativ" for s in g["sources"])
    # the explanation reaches the reply
    assert "Akkusativ" in r["reply"]


def test_exercise_agent_sets_an_exercise():
    r = graph.run_turn("u", "Erklär mir den Akkusativ", level="A1", thread_id="t")
    assert r["exercise"] is not None
    assert r["exercise"].get("type")


def test_practice_intent_only_generates():
    r = graph.run_turn("u", "Gib mir eine Übung bitte", level="A1", thread_id="t")
    assert r["plan"] == ["exercise"]
    assert agents(r) == ["planner", "exercise_generator"]


def test_memory_grades_the_exercise_set_on_a_previous_turn():
    # Turn 1: coach explains and sets an exercise (held in the checkpointed state).
    r1 = graph.run_turn("u", "Erklär mir den Akkusativ", level="A1", thread_id="mem")
    ex = r1["exercise"]
    assert ex is not None

    # Turn 2: learner submits an answer WITHOUT resending the exercise — the coach must
    # remember it. A deliberately wrong index should be graded wrong.
    sol = ex.get("solution") or {}
    assert "correctIndex" in sol
    wrong = {"selectedIndex": (sol["correctIndex"] + 1) % 4}
    r2 = graph.run_turn("u", "meine Antwort", submission=wrong, thread_id="mem")

    assert r2["plan"] == ["evaluate", "exercise"]
    assert "evaluator" in agents(r2)
    assert r2["evaluation"]["isCorrect"] is False
    assert r2["evaluation"]["scorePercent"] == 0.0


def test_correct_answer_is_graded_correct():
    r1 = graph.run_turn("u", "Übung zum Akkusativ bitte", level="A1", thread_id="ok")
    ex = r1["exercise"]
    sol = ex.get("solution") or {}
    right = {"selectedIndex": sol["correctIndex"]}
    r2 = graph.run_turn("u", "Antwort", submission=right, thread_id="ok")
    assert r2["evaluation"]["isCorrect"] is True
    assert r2["evaluation"]["scorePercent"] == 100.0


def test_weak_topics_accumulate_and_dedupe_across_turns():
    graph.run_turn("u", "Erklär mir den Akkusativ", level="A1", thread_id="w")
    r = graph.run_turn("u", "Und was ist mit dem Akkusativ noch?", level="A1", thread_id="w")
    # Same topic asked twice: recorded once, and persisted across the two turns.
    assert r["weakTopics"].count("Akkusativ") == 1


def test_reply_contains_only_this_turns_messages():
    graph.run_turn("u", "Erklär mir den Akkusativ", level="A1", thread_id="turnsep")
    r2 = graph.run_turn("u", "Gib mir noch eine Übung", level="A1", thread_id="turnsep")
    # Turn 2's steps must not include turn 1's planner/agents.
    assert all(s["turn"] == 2 for s in r2["steps"])


def test_threads_are_isolated():
    graph.run_turn("u", "Erklär mir den Akkusativ", level="A1", thread_id="A")
    rb = graph.run_turn("u", "meine Antwort", submission={"selectedIndex": 0}, thread_id="B")
    # Thread B never set an exercise, so grading falls back to an empty exercise, not A's.
    assert rb["evaluation"] is not None  # still returns a result, doesn't crash


def test_empty_message_still_returns_a_reply():
    r = graph.run_turn("u", "", level="A1", thread_id="empty")
    assert isinstance(r["reply"], str) and r["reply"]
    assert r["done"] is True


def test_turn_reports_observability_metrics():
    r = graph.run_turn("u", "Erklär mir den Akkusativ", level="A1", thread_id="obs")
    m = r["metrics"]
    assert m["latencyMs"] >= 0
    assert "llmCalls" in m and "totalTokens" in m
    # keyless: no model calls, so no tokens spent
    assert m["llmCalls"] == 0
    assert m["totalTokens"] == 0
    assert m["tracing"] is False


def test_llm_path_records_tokens_and_prompt_version(monkeypatch):
    """The with-key path: a model call records token usage and stamps the trace config with the
    prompt version. Uses a fake model so no real API call is made."""
    class FakeResp:
        content = "grammar"
        usage_metadata = {"input_tokens": 12, "output_tokens": 3}

    class FakeModel:
        def invoke(self, prompt, config=None):
            tags = (config or {}).get("tags", [])
            assert any("prompt:coach.intent@" in t for t in tags), "prompt version must tag the call"
            return FakeResp()

    monkeypatch.setenv("ANTHROPIC_API_KEY", "test-key")
    monkeypatch.setattr(llm, "_model", lambda **kw: FakeModel())

    obs.start_run()
    intent = llm.classify_intent("Sag mir bitte etwas dazu", has_submission=False)  # ambiguous → LLM
    assert intent == "grammar"

    m = obs.metrics_dict()
    assert m["llmCalls"] == 1
    assert m["totalTokens"] == 15
    assert any("coach.intent@" in v for v in m["promptVersions"])
