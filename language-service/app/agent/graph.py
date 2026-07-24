"""The Study Coach graph.

A supervisor-style multi-agent system: a `planner` turns the learner's message into an ordered
plan of agent steps; a `supervisor` walks that plan and routes to the right specialist
(`grammar_explainer`, `exercise_generator`, `evaluator`); each specialist invokes one tool and
appends to a shared transcript; a `finalize` node composes the reply. A checkpointer persists the
state per thread, so the coach remembers the exercise it set and the topics a learner struggles
with across turns.

Design choices worth noting:
- Agents coordinate through shared state, not by calling each other — adding an agent is a node
  plus a plan entry, nothing else.
- `steps` accumulates across the whole thread (an audit/memory log); each entry is stamped with a
  turn number so a single reply only shows the current turn.
- Every node has a deterministic path, so the graph runs — and is tested — with no API key.
"""
import logging
import os

from langgraph.graph import END, START, StateGraph

from . import llm, observability as obs, tools
from .state import MAX_HOPS, CoachState

logger = logging.getLogger("language-service")


# ---------------- nodes ----------------

def planner(state: CoachState) -> dict:
    """Classify intent and lay out the agent steps for this turn."""
    turn = state.get("turn", 0) + 1
    intent = llm.classify_intent(state.get("user_message", ""), state.get("submission") is not None)
    plans = {
        # grade the remembered answer, then set a fresh exercise to keep practising
        "evaluate": ["evaluate", "exercise"],
        # explain from our own lessons, then hand over a targeted exercise
        "grammar": ["grammar", "exercise"],
        "practice": ["exercise"],
        "chat": ["grammar"],
    }
    plan = plans.get(intent, ["grammar"])
    return {
        "turn": turn, "plan": plan, "step_index": 0, "hops": 0, "route": "",
        # grammar/evaluation are per-turn working artifacts — clear them so an agent this turn
        # never picks up a stale topic from a previous turn. The pending `exercise` and
        # `weak_topics` are the actual cross-turn memory and are left intact.
        "grammar": None, "evaluation": None,
        "steps": [{"agent": "planner", "turn": turn, "intent": intent, "plan": plan}],
    }


def supervisor(state: CoachState) -> dict:
    """Route to the next agent in the plan, or finalize when the plan is done or we've looped
    too long."""
    plan = state.get("plan", [])
    idx = state.get("step_index", 0)
    hops = state.get("hops", 0) + 1
    if hops > MAX_HOPS or idx >= len(plan):
        return {"route": "finalize", "hops": hops}
    return {"route": plan[idx], "hops": hops}


def grammar_explainer(state: CoachState) -> dict:
    """Answer a grammar question from the app's own lessons (RAG tool)."""
    turn = state.get("turn")
    query = state.get("user_message") or state.get("goal") or ""
    res = tools.grammar_lookup(query, state.get("level"), with_answer=llm.chat_enabled())
    sources = res.get("sources") or []
    top = sources[0] if sources else None
    topic = top.get("grammarTopic") if top else None

    if res.get("answer"):
        message = res["answer"]
    elif top:
        message = f"Dazu passt „{top['title']}“: {top['text']}"
    else:
        message = "Dazu habe ich in deinem Kursmaterial keine passende Erklärung gefunden."

    out = {
        "grammar": res,
        "step_index": state.get("step_index", 0) + 1,
        "steps": [{"agent": "grammar_explainer", "turn": turn, "tool": "grammar_lookup",
                   "topic": topic, "sources": len(sources), "message": message}],
    }
    if topic:
        out["weak_topics"] = [topic]
    return out


def exercise_generator(state: CoachState) -> dict:
    """Generate one targeted exercise (generation tool), themed on the current grammar topic."""
    turn = state.get("turn")
    grammar = state.get("grammar") or {}
    src = (grammar.get("sources") or [{}])[0]
    graded = state.get("exercise") or {}
    topic = (
        src.get("grammarTopic")                                             # explained this turn
        or (graded.get("grammarTopic") if state.get("evaluation") else None)  # keep drilling after grading
        or state.get("user_message")                                        # the learner's request
        or state.get("goal") or "Grammatik"
    )
    ex = tools.make_exercise(topic, state.get("level") or "A1", "Grammar")

    message = f"Übung ({topic}): {ex.get('prompt', '')}" if ex else "Ich konnte gerade keine Übung erstellen."
    return {
        "exercise": ex,
        "step_index": state.get("step_index", 0) + 1,
        "steps": [{"agent": "exercise_generator", "turn": turn, "tool": "make_exercise",
                   "topic": topic, "exerciseType": ex.get("type") if ex else None, "message": message}],
    }


def evaluator(state: CoachState) -> dict:
    """Grade the learner's submission against the remembered exercise (grading tool)."""
    turn = state.get("turn")
    submission = state.get("submission") or {}
    # The exercise being answered is whatever the coach last set (restored from the checkpoint),
    # unless the client sent one explicitly.
    exercise = submission.get("exercise") or state.get("exercise") or {}
    result = tools.grade_answer(exercise, submission, state.get("level"))
    ok = result.get("isCorrect")
    message = ("Richtig! " if ok else "Noch nicht ganz. ") + (result.get("explanation") or "")

    out = {
        "evaluation": result,
        "step_index": state.get("step_index", 0) + 1,
        "steps": [{"agent": "evaluator", "turn": turn, "tool": "grade_answer",
                   "score": result.get("scorePercent"), "isCorrect": ok, "message": message.strip()}],
    }
    if not ok and exercise.get("grammarTopic"):
        out["weak_topics"] = [exercise["grammarTopic"]]
    return out


def finalize(state: CoachState) -> dict:
    """Compose the reply from this turn's steps only."""
    turn = state.get("turn")
    msgs = [s.get("message") for s in state.get("steps", [])
            if s.get("message") and s.get("turn") == turn]
    reply = "\n\n".join(msgs) if msgs else "Wie kann ich dir beim Deutschlernen helfen?"
    return {"reply": reply, "done": True}


def _route(state: CoachState) -> str:
    return state.get("route") or "finalize"


# ---------------- assembly ----------------

def build_checkpointer():
    """Postgres in production (persists the coach's memory across restarts), in-memory otherwise.
    Mirrors the RAG store: a transient DB problem degrades to memory rather than failing."""
    url = os.getenv("COACH_DATABASE_URL") or os.getenv("RAG_DATABASE_URL") or os.getenv("DATABASE_URL")
    if url and os.getenv("COACH_PERSIST", "1") != "0":
        try:
            from langgraph.checkpoint.postgres import PostgresSaver
            from psycopg_pool import ConnectionPool

            pool = ConnectionPool(conninfo=url, max_size=4, open=True, kwargs={"autocommit": True})
            saver = PostgresSaver(pool)
            saver.setup()
            logger.info("coach: using Postgres checkpointer")
            return saver
        except Exception as exc:  # noqa: BLE001
            logger.warning("coach: Postgres checkpointer unavailable (%s); using in-memory", exc)
    from langgraph.checkpoint.memory import MemorySaver

    return MemorySaver()


def build_graph(checkpointer=None):
    g = StateGraph(CoachState)
    g.add_node("planner", planner)
    g.add_node("supervisor", supervisor)
    g.add_node("grammar", grammar_explainer)
    g.add_node("exercise", exercise_generator)
    g.add_node("evaluate", evaluator)
    g.add_node("finalize", finalize)

    g.add_edge(START, "planner")
    g.add_edge("planner", "supervisor")
    g.add_conditional_edges("supervisor", _route, {
        "grammar": "grammar", "exercise": "exercise", "evaluate": "evaluate", "finalize": "finalize",
    })
    g.add_edge("grammar", "supervisor")
    g.add_edge("exercise", "supervisor")
    g.add_edge("evaluate", "supervisor")
    g.add_edge("finalize", END)

    return g.compile(checkpointer=checkpointer or build_checkpointer())


_app = None


def get_app():
    global _app
    if _app is None:
        _app = build_graph()
    return _app


def reset_for_tests(checkpointer=None) -> None:
    global _app
    _app = build_graph(checkpointer=checkpointer)


def run_turn(user_id: str, message: str, level: str | None = None, goal: str | None = None,
             submission: dict | None = None, thread_id: str | None = None,
             allow_ai: bool = True) -> dict:
    """One coaching turn. `thread_id` selects the memory; defaults to the user id. `allow_ai`
    lets the caller (the .NET API) withhold LLM enhancement for a Free/over-quota user while the
    deterministic coach still works."""
    app = get_app()
    thread_id = thread_id or user_id or "anon"
    config = {"configurable": {"thread_id": thread_id}, "recursion_limit": 25}

    input_state: dict = {"user_message": message or "", "submission": submission, "user_id": user_id}
    if level:
        input_state["level"] = level
    if goal:
        input_state["goal"] = goal

    llm.set_allow_ai(allow_ai)
    obs.start_run()
    try:
        final = app.invoke(input_state, config=config)
    except Exception as exc:  # noqa: BLE001 — never 500 the coach; return a usable turn
        logger.exception("coach run failed")
        return {"reply": "Es gab ein Problem. Versuche es bitte noch einmal.",
                "error": str(exc), "steps": [], "done": True, "threadId": thread_id,
                "metrics": obs.metrics_dict()}

    turn = final.get("turn")
    return {
        "reply": final.get("reply", ""),
        "plan": final.get("plan", []),
        "steps": [s for s in final.get("steps", []) if s.get("turn") == turn],
        "grammar": final.get("grammar"),
        "exercise": final.get("exercise"),
        "evaluation": final.get("evaluation"),
        "weakTopics": final.get("weak_topics", []),
        "threadId": thread_id,
        "done": final.get("done", True),
        "metrics": obs.metrics_dict(),
    }
