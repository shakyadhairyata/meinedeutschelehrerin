"""Evaluation logic: try Claude first, fall back to heuristics. Responses use
camelCase keys so they bind straight to the .NET DTOs."""
import difflib
import re

from . import claude_client, prompts
from .schemas import GenerateRequest, GenerateVocabRequest, SpeakingRequest, WritingRequest

WORD_RE = re.compile(r"\b\w+\b", re.UNICODE)


def _words(text: str) -> list[str]:
    return WORD_RE.findall(text)


# ---------------- Writing ----------------

def evaluate_writing(req: WritingRequest) -> dict:
    data = claude_client.call_json(
        prompts.WRITING_SYSTEM.format(level=req.level),
        prompts.WRITING_USER.format(prompt=req.prompt, min_words=req.min_words, text=req.text),
    )
    if data and "scorePercent" in data:
        data.setdefault("strengths", [])
        data.setdefault("corrections", [])
        data.setdefault("correctedText", req.text)
        data.setdefault("cefrEstimate", req.level)
        data.setdefault("summary", "")
        return data
    return _offline_writing(req)


def _offline_writing(req: WritingRequest) -> dict:
    wc = len(_words(req.text))
    length_score = 1.0 if req.min_words <= 0 else min(wc / req.min_words, 1.0)
    text = req.text.strip()
    capitalised = (not text) or text[0].isupper()
    punctuated = text.endswith((".", "!", "?"))
    score = round(100 * (0.6 * length_score + (0.2 if capitalised else 0) + (0.2 if punctuated else 0)), 1)

    strengths = []
    if wc >= req.min_words:
        strengths.append(f"Längenziel erreicht ({wc}/{req.min_words} Wörter).")
    if capitalised:
        strengths.append("Großschreibung am Satzanfang.")
    if punctuated:
        strengths.append("Satzzeichen am Ende vorhanden.")
    if not strengths:
        strengths.append("Du hast einen Text produziert — bau darauf auf.")

    corrections = []
    if wc < req.min_words:
        corrections.append({
            "original": "(zu kurz)",
            "correction": f"Schreibe mindestens {req.min_words} Wörter.",
            "explanation": "Entwickle deine Ideen mit mehr Details und Beispielen.",
            "category": "task",
        })

    return {
        "scorePercent": score,
        "summary": "Offline-Bewertung (starte den ANTHROPIC_API_KEY-Modus für volles KI-Feedback).",
        "strengths": strengths,
        "corrections": corrections,
        "correctedText": req.text,
        "cefrEstimate": req.level,
    }


# ---------------- Speaking ----------------

def evaluate_speaking(req: SpeakingRequest) -> dict:
    data = claude_client.call_json(
        prompts.SPEAKING_SYSTEM.format(level=req.level),
        prompts.SPEAKING_USER.format(target_text=req.target_text, transcript=req.transcript),
    )
    if data and "scorePercent" in data:
        data.setdefault("transcript", req.transcript)
        data.setdefault("pronunciationTips", [])
        data.setdefault("summary", "")
        data.setdefault("accuracyVsTarget", data.get("scorePercent", 0))
        return data
    return _offline_speaking(req)


def _offline_speaking(req: SpeakingRequest) -> dict:
    def norm(s: str) -> list[str]:
        return [w.lower() for w in _words(s)]

    target, heard = norm(req.target_text), norm(req.transcript)
    ratio = difflib.SequenceMatcher(None, target, heard).ratio() if target else 0.0
    acc = round(ratio * 100, 1)
    return {
        "scorePercent": acc,
        "transcript": req.transcript,
        "summary": "Offline-Bewertung (starte den ANTHROPIC_API_KEY-Modus für volles Aussprache-Feedback).",
        "pronunciationTips": [
            "Sprich langsam und deutlich.",
            "Betone die erste Silbe bei trennbaren Verben.",
        ],
        "accuracyVsTarget": acc,
    }


# ---------------- Exercise generation ----------------

def _v(german, english, pos, article, plural, example, theme):
    return {"german": german, "english": english, "partOfSpeech": pos,
            "article": article, "plural": plural, "example": example, "theme": theme}


# Curated, real fallback wordlists per level (used when no ANTHROPIC_API_KEY). With a key,
# Claude generates the full Goethe-scale lists; this keeps the pipeline useful offline.
OFFLINE_VOCAB: dict[str, list[dict]] = {
    "A1": [
        _v("die Schule", "school", "Nomen", "die", "die Schulen", "Die Schule beginnt um acht.", "Alltag"),
        _v("der Lehrer", "teacher", "Nomen", "der", "die Lehrer", "Der Lehrer erklärt die Grammatik.", "Beruf"),
        _v("der Hund", "dog", "Nomen", "der", "die Hunde", "Der Hund ist freundlich.", "Tiere"),
        _v("die Katze", "cat", "Nomen", "die", "die Katzen", "Die Katze schläft.", "Tiere"),
        _v("das Auto", "car", "Nomen", "das", "die Autos", "Das Auto ist schnell.", "Verkehr"),
        _v("das Buch", "book", "Nomen", "das", "die Bücher", "Ich lese ein Buch.", "Schule"),
        _v("die Tür", "door", "Nomen", "die", "die Türen", "Mach bitte die Tür zu.", "Wohnen"),
        _v("das Fenster", "window", "Nomen", "das", "die Fenster", "Das Fenster ist offen.", "Wohnen"),
        _v("die Stadt", "city", "Nomen", "die", "die Städte", "Berlin ist eine große Stadt.", "Stadt"),
        _v("essen", "to eat", "Verb", None, None, "Wir essen um eins.", "Verben"),
        _v("schlafen", "to sleep", "Verb", None, None, "Ich schlafe acht Stunden.", "Verben"),
        _v("sehen", "to see", "Verb", None, None, "Ich sehe einen Film.", "Verben"),
        _v("gelb", "yellow", "Adjektiv", None, None, "Die Banane ist gelb.", "Farben"),
        _v("schwarz", "black", "Adjektiv", None, None, "Die Katze ist schwarz.", "Farben"),
        _v("weiß", "white", "Adjektiv", None, None, "Der Schnee ist weiß.", "Farben"),
    ],
    "A2": [
        _v("das Frühstück", "breakfast", "Nomen", "das", "die Frühstücke", "Das Frühstück ist um acht.", "Essen"),
        _v("der Urlaub", "vacation", "Nomen", "der", "die Urlaube", "Wir machen Urlaub in Italien.", "Reisen"),
        _v("der Ausweis", "ID card", "Nomen", "der", "die Ausweise", "Zeig mir bitte deinen Ausweis.", "Reisen"),
        _v("der Arzt", "doctor", "Nomen", "der", "die Ärzte", "Ich gehe zum Arzt.", "Gesundheit"),
        _v("das Krankenhaus", "hospital", "Nomen", "das", "die Krankenhäuser", "Sie liegt im Krankenhaus.", "Gesundheit"),
        _v("die Erkältung", "cold (illness)", "Nomen", "die", "die Erkältungen", "Ich habe eine Erkältung.", "Gesundheit"),
        _v("mieten", "to rent", "Verb", None, None, "Wir mieten eine Wohnung.", "Wohnen"),
        _v("umziehen", "to move (house)", "Verb", None, None, "Im Mai ziehe ich um.", "Wohnen"),
        _v("reservieren", "to reserve", "Verb", None, None, "Ich reserviere einen Tisch.", "Reisen"),
        _v("pünktlich", "punctual", "Adjektiv", None, None, "Der Zug ist pünktlich.", "Alltag"),
        _v("der Termin", "appointment", "Nomen", "der", "die Termine", "Ich habe einen Termin.", "Alltag"),
        _v("das Gemüse", "vegetables", "Nomen", "das", None, "Gemüse ist gesund.", "Essen"),
    ],
    "B1": [
        _v("die Besprechung", "meeting", "Nomen", "die", "die Besprechungen", "Die Besprechung dauert lange.", "Beruf"),
        _v("der Vertrag", "contract", "Nomen", "der", "die Verträge", "Ich unterschreibe den Vertrag.", "Beruf"),
        _v("kündigen", "to quit, terminate", "Verb", None, None, "Sie hat ihren Job gekündigt.", "Beruf"),
        _v("die Mehrheit", "majority", "Nomen", "die", "die Mehrheiten", "Die Mehrheit ist dafür.", "Gesellschaft"),
        _v("recyceln", "to recycle", "Verb", None, None, "Wir recyceln Papier und Glas.", "Umwelt"),
        _v("erneuerbar", "renewable", "Adjektiv", None, None, "erneuerbare Energien", "Umwelt"),
        _v("ehrenamtlich", "voluntary", "Adjektiv", None, None, "Er arbeitet ehrenamtlich.", "Gesellschaft"),
        _v("die Bildung", "education", "Nomen", "die", None, "Bildung ist wichtig.", "Gesellschaft"),
        _v("sich engagieren", "to get involved", "Verb", None, None, "Sie engagiert sich für die Umwelt.", "Gesellschaft"),
        _v("die Umfrage", "survey", "Nomen", "die", "die Umfragen", "Eine Umfrage zeigt das Ergebnis.", "Gesellschaft"),
        _v("der Zuschuss", "subsidy", "Nomen", "der", "die Zuschüsse", "Der Staat zahlt einen Zuschuss.", "Beruf"),
        _v("der Konsum", "consumption", "Nomen", "der", None, "Der Konsum steigt.", "Gesellschaft"),
    ],
    "B2": [
        _v("die Wirtschaft", "economy", "Nomen", "die", None, "Die Wirtschaft wächst.", "Allgemein"),
        _v("die Konkurrenz", "competition", "Nomen", "die", None, "Die Konkurrenz ist groß.", "Arbeit"),
        _v("die Globalisierung", "globalization", "Nomen", "die", None, "Die Globalisierung verändert die Arbeit.", "Gesellschaft"),
        _v("die Digitalisierung", "digitalization", "Nomen", "die", None, "Die Digitalisierung schreitet voran.", "Medien"),
        _v("die Nachhaltigkeit", "sustainability", "Nomen", "die", None, "Nachhaltigkeit ist ein Ziel.", "Umwelt"),
        _v("die Effizienz", "efficiency", "Nomen", "die", None, "Wir steigern die Effizienz.", "Allgemein"),
        _v("beträchtlich", "considerable", "Adjektiv", None, None, "ein beträchtlicher Unterschied", "Allgemein"),
        _v("nachweisen", "to prove", "Verb", None, None, "Er kann seine Erfahrung nachweisen.", "Allgemein"),
        _v("die Investition", "investment", "Nomen", "die", "die Investitionen", "eine wichtige Investition", "Allgemein"),
        _v("die Krise", "crisis", "Nomen", "die", "die Krisen", "Das Land ist in der Krise.", "Allgemein"),
        _v("der Aufschwung", "upturn", "Nomen", "der", "die Aufschwünge", "ein wirtschaftlicher Aufschwung", "Allgemein"),
        _v("scheitern", "to fail", "Verb", None, None, "Das Projekt ist gescheitert.", "Allgemein"),
    ],
    "C1": [
        _v("die Errungenschaft", "achievement", "Nomen", "die", "die Errungenschaften", "eine technische Errungenschaft", "Wissenschaft"),
        _v("der Paradigmenwechsel", "paradigm shift", "Nomen", "der", "die Paradigmenwechsel", "ein Paradigmenwechsel in der Forschung", "Wissenschaft"),
        _v("die Implikation", "implication", "Nomen", "die", "die Implikationen", "die Implikationen der Studie", "Wissenschaft"),
        _v("die Ambivalenz", "ambivalence", "Nomen", "die", "die Ambivalenzen", "eine gewisse Ambivalenz", "Gesellschaft"),
        _v("divergieren", "to diverge", "Verb", None, None, "Die Meinungen divergieren stark.", "Stil"),
        _v("konvergieren", "to converge", "Verb", None, None, "Die Ergebnisse konvergieren.", "Stil"),
        _v("die Pluralität", "plurality", "Nomen", "die", None, "die Pluralität der Gesellschaft", "Gesellschaft"),
        _v("unbestreitbar", "indisputable", "Adjektiv", None, None, "ein unbestreitbarer Vorteil", "Stil"),
        _v("die Reichweite", "scope, reach", "Nomen", "die", "die Reichweiten", "die Reichweite der Reform", "Wissenschaft"),
        _v("die Diskrepanz", "discrepancy", "Nomen", "die", "die Diskrepanzen", "eine Diskrepanz zwischen Theorie und Praxis", "Wissenschaft"),
        _v("das Postulat", "postulate", "Nomen", "das", "die Postulate", "ein zentrales Postulat", "Wissenschaft"),
        _v("die Kohärenz", "coherence", "Nomen", "die", None, "die Kohärenz des Textes", "Stil"),
    ],
}


def generate_vocabulary(req: GenerateVocabRequest) -> dict:
    data = claude_client.call_json(
        prompts.VOCAB_SYSTEM.format(level=req.level),
        prompts.VOCAB_USER.format(
            count=req.count, level=req.level,
            theme_clause=(f" zum Thema {req.theme}" if req.theme else ""),
            exclude=", ".join(req.exclude[:80]) or "(keine)",
        ),
        max_tokens=3000,
    )
    if data and isinstance(data.get("items"), list) and data["items"]:
        return data
    return _offline_vocab(req)


def _offline_vocab(req: GenerateVocabRequest) -> dict:
    excluded = {w.strip().lower() for w in req.exclude}
    pool = OFFLINE_VOCAB.get(req.level.upper(), [])
    items = [v for v in pool if v["german"].strip().lower() not in excluded]
    return {"items": items[: req.count]}


def generate_exercises(req: GenerateRequest) -> dict:
    data = claude_client.call_json(
        prompts.GENERATE_SYSTEM.format(
            level=req.level, skill=req.skill, grammar_topic=req.grammar_topic or ""
        ),
        prompts.GENERATE_USER.format(
            count=req.count, topic=req.topic, grammar_topic=req.grammar_topic or "",
            skill=req.skill, level=req.level,
        ),
        max_tokens=2500,
    )
    if data and isinstance(data.get("exercises"), list):
        return data
    # Offline: a single deterministic placeholder so the pipeline is always callable.
    return {
        "exercises": [
            {
                "type": "MultipleChoice",
                "skill": req.skill,
                "prompt": f"[Offline-Beispiel] Thema: {req.topic or req.grammar_topic or req.level}",
                "content": {
                    "question": "Set ANTHROPIC_API_KEY to generate real exercises. Beispiel: Wie heißt du?",
                    "options": ["Mir geht es gut", "Ich heiße Anna", "Danke schön", "Bis bald"],
                },
                "solution": {"correctIndex": 1},
                "explanation": "Auf »Wie heißt du?« antwortet man mit dem Namen.",
                "grammarTopic": req.grammar_topic,
                "difficulty": 1,
            }
        ]
    }
