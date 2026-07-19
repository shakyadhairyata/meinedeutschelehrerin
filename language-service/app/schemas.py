"""Request models. Responses are plain dicts with camelCase keys so the .NET
client (System.Text.Json Web defaults) binds them directly to its DTOs."""
from pydantic import BaseModel, Field


class WritingRequest(BaseModel):
    prompt: str = ""
    text: str = ""
    level: str = "A1"
    min_words: int = Field(default=40, alias="min_words")


class SpeakingRequest(BaseModel):
    target_text: str = Field(default="", alias="target_text")
    transcript: str = ""
    level: str = "A1"


class GenerateRequest(BaseModel):
    level: str = "A1"
    skill: str = "Grammar"
    topic: str = ""
    grammar_topic: str | None = None
    count: int = 5


class GenerateVocabRequest(BaseModel):
    level: str = "A1"
    theme: str | None = None
    count: int = 30
    exclude: list[str] = []


class RagDoc(BaseModel):
    """One curriculum document pushed in by the .NET API for indexing."""
    level: str = "A1"
    source: str = "lesson"
    title: str = ""
    grammarTopic: str | None = None
    text: str = ""


class RagIndexRequest(BaseModel):
    docs: list[RagDoc] = []


class RagQueryRequest(BaseModel):
    query: str = ""
    level: str | None = None
    k: int = 4
    # Retrieval is always free; the grounded Claude answer is opt-in and quota-gated upstream.
    with_answer: bool = Field(default=False, alias="with_answer")
