"""Versioned prompt registry for the coach.

Prompts are treated as versioned artifacts, not string literals scattered through the code: each
has a stable name and a version, the version travels with every LLM call as a LangSmith tag/metadata
(so runs can be grouped and compared by prompt version), and it's reported in each turn's metrics.
Bump the version when you change a template — that's the unit an eval regression is attributed to.
"""
from dataclasses import dataclass


@dataclass(frozen=True)
class Prompt:
    name: str
    version: str
    template: str

    def render(self, **kwargs) -> str:
        return self.template.format(**kwargs)


REGISTRY: dict[str, Prompt] = {
    "coach.intent": Prompt(
        name="coach.intent",
        version="2026-07-24.1",
        template=(
            "Classify the German learner's message into exactly one word: "
            "grammar, practice, or chat.\nMessage: {message!r}\nAnswer with only the word."
        ),
    ),
    "coach.reply": Prompt(
        name="coach.reply",
        version="2026-07-24.1",
        template=(
            "You are a friendly German tutor for a {level} learner. In 1-2 short sentences, "
            "{kind}. Base it only on this and reply in German:\n{context}"
        ),
    ),
}


def get(name: str) -> Prompt:
    return REGISTRY[name]
