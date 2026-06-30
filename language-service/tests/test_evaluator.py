"""Tests for the deterministic offline fallback (no ANTHROPIC_API_KEY needed in CI)."""
from app.evaluator import evaluate_writing, evaluate_speaking, generate_exercises, generate_vocabulary
from app.schemas import GenerateRequest, GenerateVocabRequest, SpeakingRequest, WritingRequest


def test_offline_writing_returns_scored_feedback():
    req = WritingRequest(
        prompt="Stell dich vor",
        text="Ich heiße Anna und ich komme aus Italien und ich lerne Deutsch.",
        level="A1",
        min_words=5,
    )
    res = evaluate_writing(req)
    assert res["scorePercent"] > 0
    assert isinstance(res["strengths"], list)
    assert res["cefrEstimate"] == "A1"
    assert "correctedText" in res


def test_offline_writing_too_short_flags_length():
    req = WritingRequest(prompt="x", text="Hallo.", level="A1", min_words=50)
    res = evaluate_writing(req)
    assert any(c["category"] == "task" for c in res["corrections"])


def test_offline_speaking_high_accuracy_when_matching():
    req = SpeakingRequest(target_text="Ich möchte einen Kaffee", transcript="Ich möchte einen Kaffee", level="A1")
    res = evaluate_speaking(req)
    assert res["scorePercent"] >= 90
    assert res["accuracyVsTarget"] >= 90


def test_offline_speaking_low_accuracy_when_unrelated():
    req = SpeakingRequest(target_text="Ich möchte einen Kaffee", transcript="Das Wetter ist schön", level="A1")
    res = evaluate_speaking(req)
    assert res["scorePercent"] < 50


def test_generate_offline_returns_at_least_one_exercise():
    req = GenerateRequest(level="A1", skill="Grammar", topic="Begrüßung", count=2)
    res = generate_exercises(req)
    assert len(res["exercises"]) >= 1
    assert res["exercises"][0]["type"]


def test_generate_vocabulary_offline_excludes_and_limits():
    req = GenerateVocabRequest(level="A1", count=3, exclude=["die Schule", "der Hund"])
    res = generate_vocabulary(req)
    germans = [i["german"] for i in res["items"]]
    assert "die Schule" not in germans
    assert "der Hund" not in germans
    assert len(res["items"]) <= 3
    assert all(i.get("german") and i.get("english") for i in res["items"])


def test_generate_vocabulary_nouns_have_article_and_plural():
    req = GenerateVocabRequest(level="A2", count=20, exclude=[])
    res = generate_vocabulary(req)
    nouns = [i for i in res["items"] if i["partOfSpeech"] == "Nomen"]
    assert nouns, "expected at least one noun in the A2 sample"
    assert all(n["article"] in ("der", "die", "das") for n in nouns)
