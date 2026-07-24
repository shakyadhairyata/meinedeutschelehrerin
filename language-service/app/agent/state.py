"""Shared state for the coaching graph.

The state is what every agent reads and writes, and what the checkpointer persists between
turns — so it *is* the coach's memory. `steps` and `weak_topics` use reducers so agents can
append to them without clobbering each other's writes across the run.
"""
import operator
from typing import Annotated, Any, Optional, TypedDict


def _merge_unique(existing: list, new: list) -> list:
    """Reducer: append while keeping order and dropping duplicates (for weak_topics)."""
    out = list(existing or [])
    for item in new or []:
        if item not in out:
            out.append(item)
    return out


class CoachState(TypedDict, total=False):
    # --- inputs for this turn ---
    user_id: str
    level: str                       # learner CEFR level (A1..C1)
    goal: str                        # what the learner wants from the session
    user_message: str                # the learner's latest message
    submission: Optional[dict]       # a learner answer to grade, if any

    # --- planning / routing ---
    turn: int                        # increments each learner turn; stamps steps for this turn
    plan: list[str]                  # ordered agent steps the planner chose
    step_index: int                  # cursor into plan
    route: str                       # supervisor's current decision
    hops: int                        # internal-step guard against loops

    # --- working artifacts produced by agents ---
    grammar: Optional[dict]          # last RAG explanation
    exercise: Optional[dict]         # last generated exercise
    evaluation: Optional[dict]       # last grading result

    # --- accumulated memory (reducers) ---
    steps: Annotated[list[dict], operator.add]        # append-only trace of agent actions
    weak_topics: Annotated[list[str], _merge_unique]  # topics the learner struggled with

    # --- output for this turn ---
    reply: str                       # the coach's message back to the learner
    done: bool
    error: Optional[str]


# The coach may chain a few agents within one turn (e.g. explain -> practise), but never
# indefinitely; the supervisor stops at this many internal hops.
MAX_HOPS = 6
