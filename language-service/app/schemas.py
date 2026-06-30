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
