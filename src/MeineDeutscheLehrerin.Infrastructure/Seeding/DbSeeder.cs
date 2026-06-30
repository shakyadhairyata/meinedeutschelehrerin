using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Seeding;

/// <summary>
/// Seeds the curriculum. A1 is hand-authored across all six skills (a genuine 2-week
/// foundation); A2–C1 are seeded with level + starter units so the structure is complete
/// and navigable, ready to be filled out by the Claude generation pipeline.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var b = new CurriculumBuilder();
        BuildA1(b);
        BuildA2(b);
        BuildB1(b);
        BuildB2(b);
        BuildC1(b);

        // Idempotent per CEFR level: insert only levels whose code isn't present yet, so new
        // content ships on later deployments without wiping existing levels or user progress.
        var existing = await db.Levels.Select(l => l.Code).ToListAsync(ct);
        var toAdd = b.Levels.Where(l => !existing.Contains(l.Code)).ToList();
        if (toAdd.Count == 0) return;

        db.Levels.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
    }

    private static void BuildA1(CurriculumBuilder b)
    {
        var a1 = b.Level(CefrLevel.A1,
            "A1 – Anfänger",
            "Erste Schritte: dich vorstellen, einkaufen, über Alltag und Familie sprechen. Ziel: Goethe-Zertifikat A1 (Start Deutsch 1).",
            "Goethe-Zertifikat A1: Start Deutsch 1");

        // ---------------- Unit 1: Begrüßung & Vorstellung ----------------
        var u1 = b.Unit(a1, "Begrüßung & Vorstellung", "Sag Hallo, stelle dich vor und buchstabiere deinen Namen.", "Kennenlernen");

        var l1g = b.Lesson(u1, "Das Verb »sein« und Personalpronomen", SkillType.Grammar,
            "## Das Verb »sein« (Präsens)\n\n| Person | Form |\n|---|---|\n| ich | **bin** |\n| du | **bist** |\n| er/sie/es | **ist** |\n| wir | **sind** |\n| ihr | **seid** |\n| sie/Sie | **sind** |\n\n*Beispiel:* **Ich bin** Anna. **Du bist** Student. **Wir sind** aus Indien.",
            grammarTopic: "Verb sein (Präsens)");
        b.Ex(l1g, ExerciseType.Conjugation, SkillType.Grammar, "Konjugiere »sein«: ___ (du)",
            new { verb = "sein", person = "du", tense = "Präsens" }, new { answers = new[] { "bist" } },
            "»du« + sein = **bist**.");
        b.Ex(l1g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze die richtige Form von »sein«.",
            new { text = "Ich ___ Lehrer und du ___ Student.", blanks = new[] { new { hint = "ich" }, new { hint = "du" } } },
            new { answers = new[] { new[] { "bin" }, new[] { "bist" } } },
            "ich **bin**, du **bist**.");
        var l1g_mc = b.Ex(l1g, ExerciseType.MultipleChoice, SkillType.Grammar, "Wähle die richtige Form: »Wir ___ aus Deutschland.«",
            new { question = "Wir ___ aus Deutschland.", options = new[] { "bin", "bist", "sind", "seid" } },
            new { correctIndex = 2 }, "»wir« + sein = **sind**.");

        var l1v = b.Lesson(u1, "Begrüßungen & Höflichkeit", SkillType.Vocabulary,
            "## Wichtige Ausdrücke\n\n- **Hallo / Guten Tag** – Hello\n- **Guten Morgen / Guten Abend** – Good morning / evening\n- **Tschüss / Auf Wiedersehen** – Bye / Goodbye\n- **Wie geht's?** – How are you?\n- **Danke, gut.** – Thanks, good.",
            grammarTopic: "Begrüßungen");
        b.Ex(l1v, ExerciseType.Matching, SkillType.Vocabulary, "Ordne Deutsch und Englisch zu.",
            new { left = new[] { "Guten Morgen", "Tschüss", "Danke", "Wie geht's?" },
                  right = new[] { "Bye", "Thank you", "Good morning", "How are you?" } },
            new { pairs = new[] { new[] { 0, 2 }, new[] { 1, 0 }, new[] { 2, 1 }, new[] { 3, 3 } } },
            "Guten Morgen = Good morning; Tschüss = Bye; Danke = Thank you; Wie geht's? = How are you?");

        var l1l = b.Lesson(u1, "Hörverstehen: Ein erstes Gespräch", SkillType.Listening,
            "Hör das Gespräch und beantworte die Frage. (Tippe auf 🔊, um den Text zu hören.)",
            grammarTopic: "Kennenlernen",
            audio: "Hallo! Ich heiße Lena. Wie heißt du? – Ich heiße Tom. Woher kommst du, Lena? – Ich komme aus Österreich.");
        b.Ex(l1l, ExerciseType.ListeningComprehension, SkillType.Listening, "Woher kommt Lena?",
            new { audioText = "Hallo! Ich heiße Lena. Wie heißt du? – Ich heiße Tom. Woher kommst du, Lena? – Ich komme aus Österreich.",
                  question = "Woher kommt Lena?", options = new[] { "aus Deutschland", "aus Österreich", "aus der Schweiz", "aus Indien" } },
            new { correctIndex = 1 }, "Lena sagt: »Ich komme aus Österreich.«");
        b.Ex(l1l, ExerciseType.Dictation, SkillType.Listening, "Schreib den Satz, den du hörst.",
            new { audioText = "Ich heiße Tom." }, new { text = "Ich heiße Tom." },
            "Achte auf Großschreibung und den Punkt: »Ich heiße Tom.«");

        var l1s = b.Lesson(u1, "Sprechen: Stell dich vor", SkillType.Speaking,
            "Stell dich laut vor. Sag deinen Namen, woher du kommst und welche Sprachen du sprichst. Nimm dich auf – das System gibt dir Feedback.",
            grammarTopic: "Vorstellung");
        b.Ex(l1s, ExerciseType.Speaking, SkillType.Speaking, "Sprich: Stell dich in 2–3 Sätzen vor.",
            new { targetText = "Hallo, ich heiße ... Ich komme aus ... und ich lerne Deutsch.", prompt = "Stell dich vor." },
            new { }, "Sprich klar und in ganzen Sätzen. Vorbild: »Hallo, ich heiße Maria. Ich komme aus Spanien und ich lerne Deutsch.«");

        var l1w = b.Lesson(u1, "Schreiben: Eine kurze Vorstellung", SkillType.Writing,
            "Schreib 3–4 Sätze über dich: Name, Herkunft, Sprache, Beruf.", grammarTopic: "Vorstellung");
        b.Ex(l1w, ExerciseType.Writing, SkillType.Writing, "Schreib eine kurze Selbstvorstellung (mind. 30 Wörter).",
            new { prompt = "Stell dich schriftlich vor: Name, Herkunft, Sprachen, Beruf.", minWords = 30 },
            new { }, "Nutze: »Ich heiße …«, »Ich komme aus …«, »Ich spreche …«, »Ich bin … von Beruf.«");

        // ---------------- Unit 2: Zahlen, Datum & Uhrzeit ----------------
        var u2 = b.Unit(a1, "Zahlen, Datum & Uhrzeit", "Zahlen 0–100, nach Telefonnummer und Uhrzeit fragen.", "Zahlen");

        var l2v = b.Lesson(u2, "Die Zahlen 0–20", SkillType.Vocabulary,
            "## Zahlen\n0 null · 1 eins · 2 zwei · 3 drei · 4 vier · 5 fünf · 6 sechs · 7 sieben · 8 acht · 9 neun · 10 zehn · 11 elf · 12 zwölf · 13 dreizehn · 20 zwanzig",
            grammarTopic: "Zahlen");
        b.Ex(l2v, ExerciseType.MultipleChoice, SkillType.Vocabulary, "Was ist »zwölf«?",
            new { question = "zwölf = ?", options = new[] { "2", "10", "12", "20" } },
            new { correctIndex = 2 }, "zwölf = 12.");
        b.Ex(l2v, ExerciseType.FillInBlank, SkillType.Vocabulary, "Schreib die Zahl als Wort: 7",
            new { text = "7 = ___", blanks = new[] { new { hint = "Zahl als Wort" } } },
            new { answers = new[] { new[] { "sieben" } } }, "7 = **sieben**.");

        var l2g = b.Lesson(u2, "W-Fragen: Wie, Was, Wo, Woher", SkillType.Grammar,
            "## W-Fragen\nDas Fragewort steht am Anfang, dann das Verb.\n\n- **Wie** heißt du?\n- **Woher** kommst du?\n- **Wo** wohnst du?\n- **Was** machst du?",
            grammarTopic: "W-Fragen");
        b.Ex(l2g, ExerciseType.Reorder, SkillType.Grammar, "Bring die Wörter in die richtige Reihenfolge.",
            new { tokens = new[] { "kommst", "Woher", "du", "?" } },
            new { answer = "Woher kommst du?" }, "Fragewort + Verb + Subjekt: »Woher kommst du?«");
        b.Ex(l2g, ExerciseType.MultipleChoice, SkillType.Grammar, "Welches Fragewort passt? »___ wohnst du?«",
            new { question = "___ wohnst du? (Antwort: in Berlin)", options = new[] { "Wie", "Wo", "Was", "Wer" } },
            new { correctIndex = 1 }, "Antwort »in Berlin« = Ort → **Wo**.");

        var l2l = b.Lesson(u2, "Hörverstehen: Die Telefonnummer", SkillType.Listening,
            "Hör zu und schreib die Telefonnummer.", grammarTopic: "Zahlen",
            audio: "Meine Telefonnummer ist null – eins – sieben – drei – vier – zwei.");
        b.Ex(l2l, ExerciseType.Dictation, SkillType.Listening, "Schreib die Nummer als Ziffern (z. B. 017342).",
            new { audioText = "null eins sieben drei vier zwei" }, new { text = "017342" },
            "null=0, eins=1, sieben=7, drei=3, vier=4, zwei=2 → 017342.");

        var l2r = b.Lesson(u2, "Lesen: Ein Stundenplan", SkillType.Reading,
            "## Lies den Text\nAnna steht um 7 Uhr auf. Der Deutschkurs beginnt um 9 Uhr und endet um 12 Uhr. Am Nachmittag um 15 Uhr trifft sie eine Freundin.",
            grammarTopic: "Uhrzeit");
        b.Ex(l2r, ExerciseType.ReadingComprehension, SkillType.Reading, "Wann beginnt der Deutschkurs?",
            new { question = "Wann beginnt der Deutschkurs?", options = new[] { "um 7 Uhr", "um 9 Uhr", "um 12 Uhr", "um 15 Uhr" } },
            new { correctIndex = 1 }, "Im Text: »Der Deutschkurs beginnt um 9 Uhr.«");

        // ---------------- Unit 3: Familie & Possessivartikel ----------------
        var u3 = b.Unit(a1, "Familie & Possessivartikel", "Über deine Familie sprechen, mein/dein/sein benutzen.", "Familie");

        var l3g = b.Lesson(u3, "Possessivartikel: mein, dein, sein, ihr", SkillType.Grammar,
            "## Possessivartikel (Nominativ)\n\n| | maskulin/neutrum | feminin/Plural |\n|---|---|---|\n| ich | mein | meine |\n| du | dein | deine |\n| er | sein | seine |\n| sie | ihr | ihre |\n\n*Beispiel:* **Mein** Bruder, **meine** Schwester.",
            grammarTopic: "Possessivartikel");
        b.Ex(l3g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze den Possessivartikel (ich).",
            new { text = "Das ist ___ Bruder und das ist ___ Schwester.", blanks = new[] { new { hint = "mask." }, new { hint = "fem." } } },
            new { answers = new[] { new[] { "mein" }, new[] { "meine" } } },
            "Bruder (m) → mein; Schwester (f) → meine.");
        b.Ex(l3g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Wie heißt ___ Mutter?« (du)",
            new { question = "Wie heißt ___ Mutter?", options = new[] { "dein", "deine", "mein", "sein" } },
            new { correctIndex = 1 }, "Mutter (f) + du → **deine**.");

        var l3v = b.Lesson(u3, "Wortschatz: Die Familie", SkillType.Vocabulary,
            "## Familie\nder Vater · die Mutter · der Bruder · die Schwester · die Eltern · das Kind · die Großeltern",
            grammarTopic: "Familie");
        b.Ex(l3v, ExerciseType.Matching, SkillType.Vocabulary, "Ordne zu.",
            new { left = new[] { "der Vater", "die Mutter", "die Schwester", "das Kind" },
                  right = new[] { "the mother", "the child", "the father", "the sister" } },
            new { pairs = new[] { new[] { 0, 2 }, new[] { 1, 0 }, new[] { 2, 3 }, new[] { 3, 1 } } },
            "Vater=father, Mutter=mother, Schwester=sister, Kind=child.");

        var l3w = b.Lesson(u3, "Schreiben: Meine Familie", SkillType.Writing,
            "Beschreib deine Familie in 4–5 Sätzen.", grammarTopic: "Familie");
        b.Ex(l3w, ExerciseType.Writing, SkillType.Writing, "Schreib über deine Familie (mind. 35 Wörter).",
            new { prompt = "Wer gehört zu deiner Familie? Wie heißen sie? Was machen sie?", minWords = 35 },
            new { }, "Nutze Possessivartikel: »Mein Vater heißt …«, »Meine Schwester ist …«.");

        // ---------------- Unit 4: Essen & Akkusativ ----------------
        var u4 = b.Unit(a1, "Essen & der Akkusativ", "Essen bestellen und den Akkusativ benutzen.", "Essen");

        var l4g = b.Lesson(u4, "Der Akkusativ: ein → einen", SkillType.Grammar,
            "## Akkusativ (unbestimmter Artikel)\nNur **maskulin** ändert sich!\n\n| | Nominativ | Akkusativ |\n|---|---|---|\n| m | ein Apfel | **einen** Apfel |\n| f | eine Banane | eine Banane |\n| n | ein Brot | ein Brot |\n\n*Ich möchte **einen** Kaffee und **eine** Cola.*",
            grammarTopic: "Akkusativ");
        b.Ex(l4g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze ein/eine/einen.",
            new { text = "Ich nehme ___ Apfel (m) und ___ Banane (f).", blanks = new[] { new { hint = "Akk. m" }, new { hint = "Akk. f" } } },
            new { answers = new[] { new[] { "einen" }, new[] { "eine" } } },
            "maskulin im Akkusativ → einen; feminin bleibt eine.");
        var l4g_mc = b.Ex(l4g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich möchte ___ Tee.« (der Tee)",
            new { question = "Ich möchte ___ Tee.", options = new[] { "ein", "eine", "einen", "der" } },
            new { correctIndex = 2 }, "der Tee (m) → Akkusativ **einen**.");
        b.Ex(l4g, ExerciseType.Reorder, SkillType.Grammar, "Bilde den Satz.",
            new { tokens = new[] { "möchte", "Ich", "einen", "Kaffee" } },
            new { answer = "Ich möchte einen Kaffee" }, "Subjekt + Verb + Akkusativobjekt: »Ich möchte einen Kaffee.«");

        var l4v = b.Lesson(u4, "Wortschatz: Essen & Trinken", SkillType.Vocabulary,
            "## Essen & Trinken\nder Apfel · das Brot · der Käse · die Milch · der Kaffee · das Wasser · die Suppe",
            grammarTopic: "Essen");
        b.Ex(l4v, ExerciseType.Matching, SkillType.Vocabulary, "Ordne zu.",
            new { left = new[] { "das Brot", "der Käse", "die Milch", "das Wasser" },
                  right = new[] { "cheese", "water", "bread", "milk" } },
            new { pairs = new[] { new[] { 0, 2 }, new[] { 1, 0 }, new[] { 2, 3 }, new[] { 3, 1 } } },
            "Brot=bread, Käse=cheese, Milch=milk, Wasser=water.");

        var l4r = b.Lesson(u4, "Lesen: Im Café", SkillType.Reading,
            "## Lies den Dialog\nKellner: Guten Tag, was möchten Sie? — Gast: Ich möchte einen Kaffee und ein Stück Kuchen, bitte. — Kellner: Gern. Sonst noch etwas? — Gast: Nein, danke.",
            grammarTopic: "Essen");
        b.Ex(l4r, ExerciseType.ReadingComprehension, SkillType.Reading, "Was bestellt der Gast?",
            new { question = "Was bestellt der Gast?", options = new[] { "Tee und Brot", "Kaffee und Kuchen", "Wasser und Suppe", "Milch und Käse" } },
            new { correctIndex = 1 }, "Der Gast möchte »einen Kaffee und ein Stück Kuchen«.");

        var l4s = b.Lesson(u4, "Sprechen: Im Restaurant bestellen", SkillType.Speaking,
            "Bestell laut etwas zu essen und zu trinken.", grammarTopic: "Essen");
        b.Ex(l4s, ExerciseType.Speaking, SkillType.Speaking, "Sprich: Bestelle ein Getränk und ein Essen.",
            new { targetText = "Ich möchte bitte einen Kaffee und ein Stück Kuchen.", prompt = "Bestelle im Café." },
            new { }, "Benutze »Ich möchte bitte einen/eine/ein …«.");

        // ---------------- Unit 5: Tagesablauf & trennbare Verben ----------------
        var u5 = b.Unit(a1, "Tagesablauf & trennbare Verben", "Über deinen Tag sprechen, trennbare Verben benutzen.", "Alltag");

        var l5g = b.Lesson(u5, "Trennbare Verben", SkillType.Grammar,
            "## Trennbare Verben\nDas Präfix steht am **Satzende**.\n\n- aufstehen → Ich **stehe** um 7 Uhr **auf**.\n- einkaufen → Ich **kaufe** am Abend **ein**.\n- fernsehen → Ich **sehe** abends **fern**.",
            grammarTopic: "Trennbare Verben");
        b.Ex(l5g, ExerciseType.Reorder, SkillType.Grammar, "Bilde den Satz (aufstehen).",
            new { tokens = new[] { "stehe", "Ich", "um 7 Uhr", "auf" } },
            new { answer = "Ich stehe um 7 Uhr auf" }, "Verb an Position 2, Präfix »auf« ans Ende.");
        b.Ex(l5g, ExerciseType.FillInBlank, SkillType.Grammar, "Trenne das Verb »einkaufen«.",
            new { text = "Ich ___ am Samstag ___.", blanks = new[] { new { hint = "Verbstamm" }, new { hint = "Präfix" } } },
            new { answers = new[] { new[] { "kaufe" }, new[] { "ein" } } },
            "einkaufen → ich **kaufe** … **ein**.");

        var l5l = b.Lesson(u5, "Hörverstehen: Tims Tag", SkillType.Listening,
            "Hör zu und beantworte die Frage.", grammarTopic: "Alltag",
            audio: "Tim steht um sechs Uhr auf. Er frühstückt und fährt um sieben Uhr zur Arbeit. Am Abend kauft er ein und sieht fern.");
        b.Ex(l5l, ExerciseType.ListeningComprehension, SkillType.Listening, "Wann steht Tim auf?",
            new { audioText = "Tim steht um sechs Uhr auf. Er frühstückt und fährt um sieben Uhr zur Arbeit. Am Abend kauft er ein und sieht fern.",
                  question = "Wann steht Tim auf?", options = new[] { "um sechs Uhr", "um sieben Uhr", "am Abend", "um acht Uhr" } },
            new { correctIndex = 0 }, "»Tim steht um sechs Uhr auf.«");

        var l5w = b.Lesson(u5, "Schreiben: Mein Tagesablauf", SkillType.Writing,
            "Beschreib deinen Tag von morgens bis abends.", grammarTopic: "Alltag");
        b.Ex(l5w, ExerciseType.Writing, SkillType.Writing, "Schreib deinen Tagesablauf (mind. 40 Wörter).",
            new { prompt = "Was machst du morgens, mittags und abends? Nutze Zeitangaben und trennbare Verben.", minWords = 40 },
            new { }, "Beispiel: »Ich stehe um 7 Uhr auf. Dann frühstücke ich. Am Abend kaufe ich ein.«");

        // ---------------- Unit 6: Einkaufen & Modalverben ----------------
        var u6 = b.Unit(a1, "Einkaufen & Modalverben", "Einkaufen gehen und Modalverben benutzen.", "Einkaufen");

        var l6g = b.Lesson(u6, "Modalverben: können, möchten, müssen", SkillType.Grammar,
            "## Modalverben\nDas Modalverb steht auf Position 2, das zweite Verb im **Infinitiv am Ende**.\n\n- Ich **kann** gut Deutsch **sprechen**.\n- Ich **möchte** ein Brot **kaufen**.\n- Ich **muss** heute **arbeiten**.",
            grammarTopic: "Modalverben");
        b.Ex(l6g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich ___ ein Kilo Äpfel kaufen.« (Wunsch)",
            new { question = "Ich ___ ein Kilo Äpfel kaufen.", options = new[] { "kann", "möchte", "muss", "bist" } },
            new { correctIndex = 1 }, "Wunsch → **möchte**.");
        b.Ex(l6g, ExerciseType.Reorder, SkillType.Grammar, "Bilde den Satz.",
            new { tokens = new[] { "kann", "Ich", "Deutsch", "sprechen" } },
            new { answer = "Ich kann Deutsch sprechen" }, "Modalverb Position 2, Infinitiv am Ende: »Ich kann Deutsch sprechen.«");

        var l6v = b.Lesson(u6, "Wortschatz: Einkaufen", SkillType.Vocabulary,
            "## Einkaufen\nder Supermarkt · das Geld · die Kasse · billig · teuer · das Kilo · die Tüte",
            grammarTopic: "Einkaufen");
        b.Ex(l6v, ExerciseType.MultipleChoice, SkillType.Vocabulary, "Gegenteil von »teuer«?",
            new { question = "teuer ↔ ?", options = new[] { "billig", "groß", "neu", "schnell" } },
            new { correctIndex = 0 }, "teuer ↔ **billig**.");

        var l6r = b.Lesson(u6, "Lesen: An der Kasse", SkillType.Reading,
            "## Lies den Dialog\nVerkäuferin: Das macht 4,50 Euro. — Kunde: Hier sind 5 Euro. — Verkäuferin: Danke, und 50 Cent zurück. Einen schönen Tag!",
            grammarTopic: "Einkaufen");
        b.Ex(l6r, ExerciseType.ReadingComprehension, SkillType.Reading, "Wie viel kostet es?",
            new { question = "Wie viel muss der Kunde bezahlen?", options = new[] { "5 Euro", "50 Cent", "4,50 Euro", "45 Euro" } },
            new { correctIndex = 2 }, "»Das macht 4,50 Euro.«");

        // ---------------- Unit 7: Artikel, Negation & Fragen ----------------
        var u7 = b.Unit(a1, "Artikel, Negation & Fragen", "Artikel, Verneinung mit nicht/kein und Ja/Nein-Fragen.", "Grundlagen");

        var l7g1 = b.Lesson(u7, "Bestimmter & unbestimmter Artikel", SkillType.Grammar,
            "## Artikel\n**Bestimmt** (the): der (m), die (f), das (n), die (Plural).\n**Unbestimmt** (a/an): ein (m/n), eine (f).\n\n*der Tisch → ein Tisch · die Lampe → eine Lampe · das Buch → ein Buch*",
            grammarTopic: "Artikel", minutes: 15);
        b.Ex(l7g1, ExerciseType.MultipleChoice, SkillType.Grammar, "Welcher bestimmte Artikel? »___ Frau«",
            new { question = "___ Frau", options = new[] { "der", "die", "das", "ein" } },
            new { correctIndex = 1 }, "»Frau« ist feminin → **die** Frau.", difficulty: 1);
        b.Ex(l7g1, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze den bestimmten Artikel (n).",
            new { text = "___ Kind spielt im Garten.", blanks = new[] { new { hint = "neutrum" } } },
            new { answers = new[] { new[] { "das", "Das" } } }, "»Kind« ist neutrum → **das** Kind.", difficulty: 1);

        var l7g2 = b.Lesson(u7, "Negation: nicht und kein", SkillType.Grammar,
            "## Verneinung\n**nicht** verneint Verben, Adjektive und ganze Sätze: »Ich komme **nicht**.«\n**kein-** verneint Nomen (mit ein/ohne Artikel): »Ich habe **kein** Auto.«, »Ich habe **keine** Zeit.«",
            grammarTopic: "Negation", minutes: 15);
        b.Ex(l7g2, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich habe ___ Zeit.« (die Zeit)",
            new { question = "Ich habe ___ Zeit.", options = new[] { "nicht", "kein", "keine", "nein" } },
            new { correctIndex = 2 }, "Nomen + feminin → **keine** Zeit.", difficulty: 2);
        b.Ex(l7g2, ExerciseType.FillInBlank, SkillType.Grammar, "Verneine das Nomen (n).",
            new { text = "Das ist ___ Problem.", blanks = new[] { new { hint = "kein-" } } },
            new { answers = new[] { new[] { "kein" } } }, "»Problem« (n) → **kein** Problem.", difficulty: 2);

        var l7g3 = b.Lesson(u7, "Ja/Nein-Fragen & Satzbau", SkillType.Grammar,
            "## Ja/Nein-Fragen\nDas **Verb steht am Anfang**: »**Kommst** du mit?« → »Ja.« / »Nein.«\nIm Aussagesatz steht das Verb auf **Position 2**: »Du **kommst** mit.«",
            grammarTopic: "Satzbau", minutes: 15);
        b.Ex(l7g3, ExerciseType.Reorder, SkillType.Grammar, "Bilde eine Ja/Nein-Frage.",
            new { tokens = new[] { "du", "Kommst", "mit", "?" } },
            new { answer = "Kommst du mit?" }, "Verb am Anfang: »Kommst du mit?«", difficulty: 2);
        b.Ex(l7g3, ExerciseType.MultipleChoice, SkillType.Grammar, "Welche Ja/Nein-Frage ist korrekt?",
            new { question = "Wähle die richtige Frage.", options = new[] { "Du sprichst Deutsch?", "Sprichst du Deutsch?", "Deutsch du sprichst?", "Sprechen du Deutsch?" } },
            new { correctIndex = 1 }, "Verb zuerst: »Sprichst du Deutsch?«", difficulty: 2);

        // ---------------- Vocabulary deck (themed) ----------------
        b.Vocab(a1, "der Apfel", "apple", "Nomen", "der", "die Äpfel", "Ich esse einen Apfel.", "Essen");
        b.Vocab(a1, "das Brot", "bread", "Nomen", "das", "die Brote", "Das Brot ist frisch.", "Essen");
        b.Vocab(a1, "der Kaffee", "coffee", "Nomen", "der", "die Kaffees", "Ich trinke gern Kaffee.", "Essen");
        b.Vocab(a1, "die Familie", "family", "Nomen", "die", "die Familien", "Meine Familie ist groß.", "Familie");
        b.Vocab(a1, "der Bruder", "brother", "Nomen", "der", "die Brüder", "Mein Bruder heißt Max.", "Familie");
        b.Vocab(a1, "die Schwester", "sister", "Nomen", "die", "die Schwestern", "Meine Schwester ist Ärztin.", "Familie");
        b.Vocab(a1, "wohnen", "to live", "Verb", null, null, "Ich wohne in Berlin.", "Alltag");
        b.Vocab(a1, "arbeiten", "to work", "Verb", null, null, "Sie arbeitet im Büro.", "Alltag");
        b.Vocab(a1, "kaufen", "to buy", "Verb", null, null, "Wir kaufen Brot.", "Einkaufen");
        b.Vocab(a1, "billig", "cheap", "Adjektiv", null, null, "Das Brot ist billig.", "Einkaufen");
        b.Vocab(a1, "teuer", "expensive", "Adjektiv", null, null, "Das Auto ist teuer.", "Einkaufen");
        b.Vocab(a1, "heute", "today", "Adverb", null, null, "Heute lerne ich Deutsch.", "Zeit");
        b.Vocab(a1, "der Vater", "father", "Nomen", "der", "die Väter", "Mein Vater arbeitet viel.", "Familie");
        b.Vocab(a1, "die Mutter", "mother", "Nomen", "die", "die Mütter", "Meine Mutter kocht gern.", "Familie");
        b.Vocab(a1, "das Kind", "child", "Nomen", "das", "die Kinder", "Das Kind spielt.", "Familie");
        b.Vocab(a1, "der Mann", "man", "Nomen", "der", "die Männer", "Der Mann liest die Zeitung.", "Menschen");
        b.Vocab(a1, "die Frau", "woman", "Nomen", "die", "die Frauen", "Die Frau ist Ärztin.", "Menschen");
        b.Vocab(a1, "der Freund", "friend", "Nomen", "der", "die Freunde", "Mein Freund wohnt in Köln.", "Menschen");
        b.Vocab(a1, "das Wasser", "water", "Nomen", "das", null, "Ich trinke Wasser.", "Essen");
        b.Vocab(a1, "die Milch", "milk", "Nomen", "die", null, "Die Milch ist kalt.", "Essen");
        b.Vocab(a1, "der Tee", "tea", "Nomen", "der", "die Tees", "Möchtest du einen Tee?", "Essen");
        b.Vocab(a1, "das Ei", "egg", "Nomen", "das", "die Eier", "Ich esse ein Ei.", "Essen");
        b.Vocab(a1, "das Haus", "house", "Nomen", "das", "die Häuser", "Das Haus ist groß.", "Wohnen");
        b.Vocab(a1, "die Wohnung", "apartment", "Nomen", "die", "die Wohnungen", "Die Wohnung ist klein.", "Wohnen");
        b.Vocab(a1, "das Zimmer", "room", "Nomen", "das", "die Zimmer", "Mein Zimmer ist hell.", "Wohnen");
        b.Vocab(a1, "der Tisch", "table", "Nomen", "der", "die Tische", "Das Buch liegt auf dem Tisch.", "Wohnen");
        b.Vocab(a1, "der Stuhl", "chair", "Nomen", "der", "die Stühle", "Der Stuhl ist neu.", "Wohnen");
        b.Vocab(a1, "der Tag", "day", "Nomen", "der", "die Tage", "Einen schönen Tag!", "Zeit");
        b.Vocab(a1, "die Woche", "week", "Nomen", "die", "die Wochen", "Die Woche hat sieben Tage.", "Zeit");
        b.Vocab(a1, "das Jahr", "year", "Nomen", "das", "die Jahre", "Ich lerne seit einem Jahr Deutsch.", "Zeit");
        b.Vocab(a1, "gut", "good", "Adjektiv", null, null, "Das Essen ist gut.", "Adjektive");
        b.Vocab(a1, "groß", "big, tall", "Adjektiv", null, null, "Berlin ist groß.", "Adjektive");
        b.Vocab(a1, "klein", "small", "Adjektiv", null, null, "Die Katze ist klein.", "Adjektive");
        b.Vocab(a1, "neu", "new", "Adjektiv", null, null, "Mein Handy ist neu.", "Adjektive");
        b.Vocab(a1, "alt", "old", "Adjektiv", null, null, "Das Haus ist alt.", "Adjektive");
        b.Vocab(a1, "rot", "red", "Adjektiv", null, null, "Der Apfel ist rot.", "Farben");
        b.Vocab(a1, "blau", "blue", "Adjektiv", null, null, "Der Himmel ist blau.", "Farben");
        b.Vocab(a1, "grün", "green", "Adjektiv", null, null, "Das Gras ist grün.", "Farben");
        b.Vocab(a1, "gehen", "to go", "Verb", null, null, "Ich gehe nach Hause.", "Verben");
        b.Vocab(a1, "trinken", "to drink", "Verb", null, null, "Wir trinken Kaffee.", "Verben");
        b.Vocab(a1, "sprechen", "to speak", "Verb", null, null, "Sprichst du Deutsch?", "Verben");
        b.Vocab(a1, "lernen", "to learn", "Verb", null, null, "Ich lerne jeden Tag.", "Verben");

        // ---------------- Practice sets ----------------
        b.Set(a1, "A1 Grammatik-Drill", "Gemischte Grammatikübungen: sein, Akkusativ, Modalverben.",
            "drill", SkillType.Grammar, false, null,
            l1g_mc, l4g_mc);

        b.Set(a1, "A1 Modellprüfung (Start Deutsch 1)",
            "Eine kurze Modellprüfung quer durch alle Fertigkeiten. Setze dir 65 Minuten.",
            "exam", null, true, 65,
            l1g_mc, l4g_mc);
    }

    private static void BuildA2(CurriculumBuilder b)
    {
        var a2 = b.Level(CefrLevel.A2,
            "A2 – Grundlegende Kenntnisse",
            "Vergangenheit (Perfekt & Präteritum), Dativ & Wechselpräpositionen, Wegbeschreibung, Gesundheit, Vergleiche und Reisen. Ziel: Goethe-Zertifikat A2.",
            "Goethe-Zertifikat A2");

        // ---------------- Unit 1: Perfekt ----------------
        var u1 = b.Unit(a2, "Das Perfekt – über Gestern sprechen", "Über die Vergangenheit mit haben/sein + Partizip II sprechen.", "Vergangenheit");

        var l1g = b.Lesson(u1, "Das Perfekt: haben/sein + Partizip II", SkillType.Grammar,
            "## Das Perfekt\nDas Perfekt bildet man mit **haben** oder **sein** + **Partizip II** (am Satzende).\n\n- regelmäßig: machen → ge**mach**t · spielen → ge**spiel**t\n- unregelmäßig: sehen → ge**seh**en · gehen → ge**gang**en\n- mit **sein**: Bewegung/Veränderung — gehen, fahren, kommen, aufstehen\n\n*Ich **habe** Fußball **gespielt**. Ich **bin** nach Berlin **gefahren**.*",
            grammarTopic: "Perfekt", minutes: 20);
        var l1g_aux = b.Ex(l1g, ExerciseType.MultipleChoice, SkillType.Grammar, "Wähle das Hilfsverb: »Ich ___ nach Berlin gefahren.«",
            new { question = "Ich ___ nach Berlin gefahren.", options = new[] { "habe", "bin", "war", "hat" } },
            new { correctIndex = 1 }, "»fahren« ist ein Bewegungsverb → Perfekt mit **sein**: »Ich bin gefahren.«", difficulty: 2);
        b.Ex(l1g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze das Perfekt von »spielen«.",
            new { text = "Gestern ___ ich Fußball ___.", blanks = new[] { new { hint = "haben" }, new { hint = "Partizip II" } } },
            new { answers = new[] { new[] { "habe" }, new[] { "gespielt" } } },
            "haben + Partizip II: »Ich **habe** … **gespielt**.«", difficulty: 2);
        b.Ex(l1g, ExerciseType.Reorder, SkillType.Grammar, "Bilde einen Perfekt-Satz.",
            new { tokens = new[] { "habe", "Ich", "einen Film", "gesehen" } },
            new { answer = "Ich habe einen Film gesehen" }, "Hilfsverb Position 2, Partizip am Ende: »Ich habe einen Film gesehen.«", difficulty: 2);

        var l1l = b.Lesson(u1, "Hörverstehen: Das Wochenende", SkillType.Listening,
            "Hör zu und beantworte die Frage.", grammarTopic: "Perfekt",
            audio: "Am Samstag bin ich lange geschlafen. Dann habe ich Freunde getroffen und wir sind ins Kino gegangen. Am Sonntag habe ich für die Prüfung gelernt.");
        b.Ex(l1l, ExerciseType.ListeningComprehension, SkillType.Listening, "Was hat die Person am Sonntag gemacht?",
            new { audioText = "Am Samstag bin ich lange geschlafen. Dann habe ich Freunde getroffen und wir sind ins Kino gegangen. Am Sonntag habe ich für die Prüfung gelernt.",
                  question = "Was hat die Person am Sonntag gemacht?", options = new[] { "Freunde getroffen", "im Kino gewesen", "für die Prüfung gelernt", "lange geschlafen" } },
            new { correctIndex = 2 }, "»Am Sonntag habe ich für die Prüfung gelernt.«", difficulty: 2);

        var l1w = b.Lesson(u1, "Schreiben: Mein Wochenende", SkillType.Writing,
            "Schreib, was du letztes Wochenende gemacht hast — im Perfekt.", grammarTopic: "Perfekt");
        b.Ex(l1w, ExerciseType.Writing, SkillType.Writing, "Beschreib dein Wochenende (mind. 50 Wörter, im Perfekt).",
            new { prompt = "Was hast du am Samstag und Sonntag gemacht? Nutze das Perfekt (haben/sein + Partizip II).", minWords = 50 },
            new { }, "Beispiel: »Am Samstag habe ich eingekauft. Am Sonntag bin ich spazieren gegangen.«", difficulty: 2);

        // ---------------- Unit 2: Dativ & Wechselpräpositionen ----------------
        var u2 = b.Unit(a2, "Dativ & Wechselpräpositionen", "Den Dativ und die Wo/Wohin-Präpositionen benutzen.", "Grammatik");

        var l2g = b.Lesson(u2, "Der Dativ", SkillType.Grammar,
            "## Der Dativ\n| | maskulin | feminin | neutrum | Plural |\n|---|---|---|---|---|\n| Dativ | dem | der | dem | den + n |\n\nNach **mit, bei, aus, zu, von, nach, seit** und Verben wie **helfen, geben, danken**.\n\n*Ich fahre **mit dem** Bus. Ich helfe **der** Frau.*",
            grammarTopic: "Dativ", minutes: 20);
        var l2g_mc = b.Ex(l2g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich helfe ___ Mann.« (der Mann)",
            new { question = "Ich helfe ___ Mann.", options = new[] { "der", "den", "dem", "des" } },
            new { correctIndex = 2 }, "helfen + Dativ, maskulin → **dem**.", difficulty: 2);
        b.Ex(l2g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze im Dativ.",
            new { text = "Ich fahre mit ___ Bus (m) und komme aus ___ Schweiz (f).", blanks = new[] { new { hint = "Dativ m" }, new { hint = "Dativ f" } } },
            new { answers = new[] { new[] { "dem" }, new[] { "der" } } }, "mit + Dativ → dem Bus; aus + Dativ → der Schweiz.", difficulty: 2);

        var l2g2 = b.Lesson(u2, "Wechselpräpositionen: Wo? vs. Wohin?", SkillType.Grammar,
            "## Wechselpräpositionen\nin, an, auf, über, unter, vor, hinter, neben, zwischen.\n\n- **Wo?** (Position) → **Dativ**: Das Buch liegt **auf dem** Tisch.\n- **Wohin?** (Richtung) → **Akkusativ**: Ich lege das Buch **auf den** Tisch.",
            grammarTopic: "Wechselpräpositionen", minutes: 20);
        var l2g2_mc = b.Ex(l2g2, ExerciseType.MultipleChoice, SkillType.Grammar, "»Das Bild hängt an ___ Wand.« (Wo? · die Wand)",
            new { question = "Das Bild hängt an ___ Wand.", options = new[] { "die", "der", "den", "dem" } },
            new { correctIndex = 1 }, "»Wo?« → Dativ; feminin → **der** Wand.", difficulty: 3);

        var l2r = b.Lesson(u2, "Lesen: Im Büro", SkillType.Reading,
            "## Lies den Text\nLisa arbeitet in einem Büro in der Stadt. Ihr Schreibtisch steht am Fenster. Auf dem Tisch liegen ihr Laptop und viele Papiere. Neben dem Computer steht ein Foto von ihrer Familie.",
            grammarTopic: "Wechselpräpositionen");
        b.Ex(l2r, ExerciseType.ReadingComprehension, SkillType.Reading, "Wo steht das Foto?",
            new { question = "Wo steht das Foto?", options = new[] { "auf dem Tisch", "am Fenster", "neben dem Computer", "in der Stadt" } },
            new { correctIndex = 2 }, "»Neben dem Computer steht ein Foto.«", difficulty: 2);

        // ---------------- Unit 3: Wegbeschreibung & Imperativ ----------------
        var u3 = b.Unit(a2, "Wegbeschreibung & Imperativ", "Nach dem Weg fragen und Anweisungen geben.", "Stadt");

        var l3g = b.Lesson(u3, "Der Imperativ", SkillType.Grammar,
            "## Imperativ\n- du: **Geh** geradeaus! **Nimm** die erste Straße!\n- Sie: **Gehen Sie** geradeaus! **Nehmen Sie** die U-Bahn!\n- ihr: **Geht** geradeaus!",
            grammarTopic: "Imperativ", minutes: 15);
        b.Ex(l3g, ExerciseType.Reorder, SkillType.Grammar, "Bilde einen Imperativ-Satz (Sie-Form).",
            new { tokens = new[] { "Sie", "Nehmen", "die erste Straße", "rechts" } },
            new { answer = "Nehmen Sie die erste Straße rechts" }, "Verb zuerst, dann »Sie«: »Nehmen Sie die erste Straße rechts.«", difficulty: 2);

        var l3v = b.Lesson(u3, "Wortschatz: Wege & Verkehr", SkillType.Vocabulary,
            "## Wege & Verkehr\nlinks · rechts · geradeaus · die Ampel · die Kreuzung · die Haltestelle · die U-Bahn · abbiegen",
            grammarTopic: "Stadt");
        b.Ex(l3v, ExerciseType.Matching, SkillType.Vocabulary, "Ordne zu.",
            new { left = new[] { "links", "geradeaus", "die Ampel", "abbiegen" },
                  right = new[] { "to turn", "straight ahead", "left", "traffic light" } },
            new { pairs = new[] { new[] { 0, 2 }, new[] { 1, 1 }, new[] { 2, 3 }, new[] { 3, 0 } } },
            "links=left, geradeaus=straight ahead, Ampel=traffic light, abbiegen=to turn.", difficulty: 2);

        var l3s = b.Lesson(u3, "Sprechen: Nach dem Weg fragen", SkillType.Speaking,
            "Frag höflich nach dem Weg zum Bahnhof.", grammarTopic: "Stadt");
        b.Ex(l3s, ExerciseType.Speaking, SkillType.Speaking, "Sprich: Frag nach dem Weg zum Bahnhof.",
            new { targetText = "Entschuldigung, wie komme ich zum Bahnhof?", prompt = "Frag höflich nach dem Weg." },
            new { }, "Höfliche Frage: »Entschuldigung, wie komme ich zum …?«", difficulty: 2);

        // ---------------- Unit 4: Gesundheit ----------------
        var u4 = b.Unit(a2, "Gesundheit & Körper", "Über Gesundheit sprechen und Modalverben benutzen.", "Gesundheit");

        var l4v = b.Lesson(u4, "Wortschatz: Der Körper", SkillType.Vocabulary,
            "## Der Körper\nder Kopf · der Bauch · der Hals · der Rücken · das Bein · der Arm · die Hand · das Auge",
            grammarTopic: "Gesundheit");
        b.Ex(l4v, ExerciseType.Matching, SkillType.Vocabulary, "Ordne zu.",
            new { left = new[] { "der Kopf", "der Bauch", "der Rücken", "das Bein" },
                  right = new[] { "back", "leg", "head", "stomach" } },
            new { pairs = new[] { new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2, 0 }, new[] { 3, 1 } } },
            "Kopf=head, Bauch=stomach, Rücken=back, Bein=leg.", difficulty: 2);

        var l4g = b.Lesson(u4, "Modalverben: müssen, sollen, dürfen", SkillType.Grammar,
            "## Modalverben (A2)\n- **müssen** (Notwendigkeit): Du **musst** viel trinken.\n- **sollen** (Rat/Auftrag): Du **sollst** zum Arzt gehen.\n- **dürfen** (Erlaubnis): Du **darfst** heute nicht arbeiten.",
            grammarTopic: "Modalverben", minutes: 18);
        var l4g_mc = b.Ex(l4g, ExerciseType.MultipleChoice, SkillType.Grammar, "Der Arzt sagt: »Sie ___ im Bett bleiben.« (Rat)",
            new { question = "Sie ___ im Bett bleiben.", options = new[] { "dürfen", "sollen", "können", "wollen" } },
            new { correctIndex = 1 }, "Ein Rat/eine Empfehlung → **sollen**.", difficulty: 2);

        var l4r = b.Lesson(u4, "Lesen: Beim Arzt", SkillType.Reading,
            "## Lies den Dialog\nArzt: Was fehlt Ihnen? — Patient: Ich habe seit zwei Tagen Kopfschmerzen und Fieber. — Arzt: Sie haben eine Erkältung. Sie sollen viel trinken und sich ausruhen. Hier ist ein Rezept.",
            grammarTopic: "Gesundheit");
        b.Ex(l4r, ExerciseType.ReadingComprehension, SkillType.Reading, "Was soll der Patient tun?",
            new { question = "Was soll der Patient tun?", options = new[] { "mehr arbeiten", "viel trinken und sich ausruhen", "Sport machen", "nichts essen" } },
            new { correctIndex = 1 }, "»Sie sollen viel trinken und sich ausruhen.«", difficulty: 2);

        // ---------------- Unit 5: Komparativ & Superlativ ----------------
        var u5 = b.Unit(a2, "Vergleiche: Komparativ & Superlativ", "Dinge vergleichen und steigern.", "Vergleichen");

        var l5g = b.Lesson(u5, "Steigerung der Adjektive", SkillType.Grammar,
            "## Komparativ & Superlativ\n| Grundform | Komparativ | Superlativ |\n|---|---|---|\n| klein | klein**er** | am klein**sten** |\n| groß | größer | am größten |\n| gut | besser | am besten |\n| viel | mehr | am meisten |\n\n*Berlin ist **größer als** München. Berlin ist **am größten**.*",
            grammarTopic: "Komparativ", minutes: 18);
        var l5g_fb = b.Ex(l5g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze den Komparativ.",
            new { text = "Berlin ist ___ als München. (groß)", blanks = new[] { new { hint = "Komparativ" } } },
            new { answers = new[] { new[] { "größer" } } }, "groß → **größer** … als.", difficulty: 2);
        b.Ex(l5g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Anna spricht ___ Deutsch in der Klasse.« (gut, Superlativ)",
            new { question = "Anna spricht ___ Deutsch in der Klasse.", options = new[] { "besser", "am besten", "gut", "am gutsten" } },
            new { correctIndex = 1 }, "gut → besser → **am besten**.", difficulty: 3);

        var l5r = b.Lesson(u5, "Lesen: Drei Städte", SkillType.Reading,
            "## Lies den Text\nHamburg, Köln und München sind große Städte. München ist teurer als Köln, aber Hamburg hat den größten Hafen. Köln ist am ältesten und hat den berühmten Dom.",
            grammarTopic: "Komparativ");
        b.Ex(l5r, ExerciseType.ReadingComprehension, SkillType.Reading, "Welche Stadt ist am ältesten?",
            new { question = "Welche Stadt ist am ältesten?", options = new[] { "Hamburg", "München", "Köln", "alle gleich" } },
            new { correctIndex = 2 }, "»Köln ist am ältesten.«", difficulty: 2);

        // ---------------- Unit 6: Reisen & Präteritum ----------------
        var u6 = b.Unit(a2, "Reisen & Präteritum (war/hatte)", "Über Reisen erzählen und das Präteritum von sein/haben benutzen.", "Reisen");

        var l6g = b.Lesson(u6, "Präteritum: war, hatte, Modalverben", SkillType.Grammar,
            "## Präteritum (gesprochen & geschrieben)\nBei **sein, haben** und Modalverben nutzt man oft das Präteritum statt Perfekt.\n\n- sein → ich **war**, du **warst**, er **war**, wir **waren**\n- haben → ich **hatte**, du **hattest**, wir **hatten**\n- können → ich **konnte** · müssen → ich **musste**",
            grammarTopic: "Präteritum", minutes: 18);
        var l6g_fb = b.Ex(l6g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze das Präteritum.",
            new { text = "Letztes Jahr ___ ich in Wien. Es ___ super! (sein)", blanks = new[] { new { hint = "ich" }, new { hint = "es" } } },
            new { answers = new[] { new[] { "war" }, new[] { "war" } } }, "sein im Präteritum: ich **war**, es **war**.", difficulty: 2);
        b.Ex(l6g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Als Kind ___ ich kein Gemüse.« (mögen-Ersatz: essen wollen → wollen)",
            new { question = "Als Kind ___ ich kein Gemüse essen.", options = new[] { "will", "wollte", "wollen", "gewollt" } },
            new { correctIndex = 1 }, "wollen im Präteritum: ich **wollte**.", difficulty: 3);

        var l6w = b.Lesson(u6, "Schreiben: Meine letzte Reise", SkillType.Writing,
            "Erzähl von einer Reise: wohin, mit wem, was du gemacht hast.", grammarTopic: "Reisen");
        b.Ex(l6w, ExerciseType.Writing, SkillType.Writing, "Beschreib deine letzte Reise (mind. 60 Wörter).",
            new { prompt = "Wohin bist du gereist? Mit wem? Was hast du gemacht? Wie war es? (Perfekt + war/hatte)", minWords = 60 },
            new { }, "Mix aus Perfekt (»Ich bin … gefahren«) und Präteritum (»Es war schön«).", difficulty: 2);

        // ---------------- Unit 7: Reflexivverben & Zeitangaben ----------------
        var u7 = b.Unit(a2, "Reflexivverben & Zeitangaben", "Reflexivverben, temporale Präpositionen und als/wenn.", "Grammatik");

        var l7g1 = b.Lesson(u7, "Reflexivverben", SkillType.Grammar,
            "## Reflexivverben\nViele Verben brauchen ein **Reflexivpronomen**: mich, dich, sich, uns, euch, sich.\n- sich freuen: Ich freue **mich**.\n- sich waschen: Du wäschst **dich**.\n- sich treffen: Wir treffen **uns**.",
            grammarTopic: "Reflexivverben", minutes: 18);
        b.Ex(l7g1, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze das Reflexivpronomen (ich).",
            new { text = "Ich freue ___ auf den Urlaub.", blanks = new[] { new { hint = "ich" } } },
            new { answers = new[] { new[] { "mich" } } }, "ich → **mich**: »Ich freue mich.«", difficulty: 2);
        b.Ex(l7g1, ExerciseType.MultipleChoice, SkillType.Grammar, "»Wir treffen ___ um acht.«",
            new { question = "Wir treffen ___ um acht.", options = new[] { "mich", "dich", "uns", "sich" } },
            new { correctIndex = 2 }, "wir → **uns**.", difficulty: 2);

        var l7g2 = b.Lesson(u7, "Temporale Präpositionen", SkillType.Grammar,
            "## Zeitangaben mit Präpositionen\n- **am** + Tag/Datum: am Montag, am 3. Mai\n- **im** + Monat/Jahreszeit: im Juli, im Winter\n- **um** + Uhrzeit: um 8 Uhr\n- **seit** (Dauer bis jetzt): seit 2020 · **vor** (Vergangenheit): vor drei Tagen",
            grammarTopic: "Temporale Präpositionen", minutes: 18);
        b.Ex(l7g2, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze die Präpositionen.",
            new { text = "Ich stehe ___ 7 Uhr auf und im Sommer fahren wir ___ Urlaub.", blanks = new[] { new { hint = "Uhrzeit" }, new { hint = "in den" } } },
            new { answers = new[] { new[] { "um" }, new[] { "in" } } }, "um + Uhrzeit; »in den Urlaub« fahren.", difficulty: 3);
        b.Ex(l7g2, ExerciseType.MultipleChoice, SkillType.Grammar, "»___ Montag habe ich frei.«",
            new { question = "___ Montag habe ich frei.", options = new[] { "Im", "Am", "Um", "Seit" } },
            new { correctIndex = 1 }, "am + Wochentag → **Am** Montag.", difficulty: 2);

        var l7g3 = b.Lesson(u7, "»als« oder »wenn«?", SkillType.Grammar,
            "## als oder wenn?\n- **als**: einmalig in der **Vergangenheit** — »**Als** ich klein war, …«\n- **wenn**: **wiederholt** oder Gegenwart/Zukunft — »Immer **wenn** es regnet, …«",
            grammarTopic: "als/wenn", minutes: 16);
        b.Ex(l7g3, ExerciseType.MultipleChoice, SkillType.Grammar, "»___ ich gestern nach Hause kam, war niemand da.«",
            new { question = "___ ich gestern nach Hause kam, war niemand da.", options = new[] { "Wenn", "Als", "Wann", "Ob" } },
            new { correctIndex = 1 }, "einmalig + Vergangenheit → **Als**.", difficulty: 3);
        b.Ex(l7g3, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze (wiederholte Handlung).",
            new { text = "Immer ___ ich Zeit habe, lese ich.", blanks = new[] { new { hint = "wiederholt" } } },
            new { answers = new[] { new[] { "wenn" } } }, "wiederholt → **wenn**.", difficulty: 3);

        // ---------------- Vocabulary ----------------
        b.Vocab(a2, "die Reise", "trip, journey", "Nomen", "die", "die Reisen", "Die Reise war lang.", "Reisen");
        b.Vocab(a2, "der Bahnhof", "train station", "Nomen", "der", "die Bahnhöfe", "Wir treffen uns am Bahnhof.", "Reisen");
        b.Vocab(a2, "der Koffer", "suitcase", "Nomen", "der", "die Koffer", "Mein Koffer ist schwer.", "Reisen");
        b.Vocab(a2, "die Gesundheit", "health", "Nomen", "die", null, "Gesundheit ist wichtig.", "Gesundheit");
        b.Vocab(a2, "der Termin", "appointment", "Nomen", "der", "die Termine", "Ich habe einen Termin beim Arzt.", "Gesundheit");
        b.Vocab(a2, "sich ausruhen", "to rest", "Verb", null, null, "Du sollst dich ausruhen.", "Gesundheit");
        b.Vocab(a2, "abbiegen", "to turn (off)", "Verb", null, null, "Biegen Sie links ab.", "Stadt");
        b.Vocab(a2, "die Kreuzung", "intersection", "Nomen", "die", "die Kreuzungen", "An der Kreuzung rechts.", "Stadt");
        b.Vocab(a2, "vergleichen", "to compare", "Verb", null, null, "Wir vergleichen die Preise.", "Vergleichen");
        b.Vocab(a2, "der Ausflug", "excursion", "Nomen", "der", "die Ausflüge", "Wir machen einen Ausflug.", "Reisen");
        b.Vocab(a2, "treffen", "to meet", "Verb", null, null, "Ich treffe meine Freunde.", "Freizeit");
        b.Vocab(a2, "geöffnet", "open", "Adjektiv", null, null, "Das Geschäft ist geöffnet.", "Alltag");
        b.Vocab(a2, "der Zug", "train", "Nomen", "der", "die Züge", "Der Zug fährt um acht Uhr.", "Reisen");
        b.Vocab(a2, "das Flugzeug", "airplane", "Nomen", "das", "die Flugzeuge", "Das Flugzeug landet pünktlich.", "Reisen");
        b.Vocab(a2, "das Hotel", "hotel", "Nomen", "das", "die Hotels", "Wir übernachten im Hotel.", "Reisen");
        b.Vocab(a2, "die Fahrkarte", "ticket", "Nomen", "die", "die Fahrkarten", "Ich kaufe eine Fahrkarte.", "Reisen");
        b.Vocab(a2, "die Apotheke", "pharmacy", "Nomen", "die", "die Apotheken", "Die Apotheke ist um die Ecke.", "Gesundheit");
        b.Vocab(a2, "das Medikament", "medicine", "Nomen", "das", "die Medikamente", "Nimm das Medikament dreimal täglich.", "Gesundheit");
        b.Vocab(a2, "der Schmerz", "pain", "Nomen", "der", "die Schmerzen", "Ich habe Schmerzen im Rücken.", "Gesundheit");
        b.Vocab(a2, "gesund", "healthy", "Adjektiv", null, null, "Obst ist gesund.", "Gesundheit");
        b.Vocab(a2, "krank", "sick", "Adjektiv", null, null, "Ich bin heute krank.", "Gesundheit");
        b.Vocab(a2, "müde", "tired", "Adjektiv", null, null, "Ich bin sehr müde.", "Gesundheit");
        b.Vocab(a2, "die Straße", "street", "Nomen", "die", "die Straßen", "Die Straße ist breit.", "Stadt");
        b.Vocab(a2, "der Platz", "square, place", "Nomen", "der", "die Plätze", "Der Platz ist im Zentrum.", "Stadt");
        b.Vocab(a2, "das Wetter", "weather", "Nomen", "das", null, "Das Wetter ist schön.", "Alltag");
        b.Vocab(a2, "die Sonne", "sun", "Nomen", "die", "die Sonnen", "Die Sonne scheint.", "Alltag");
        b.Vocab(a2, "warm", "warm", "Adjektiv", null, null, "Es ist heute warm.", "Alltag");
        b.Vocab(a2, "kalt", "cold", "Adjektiv", null, null, "Im Winter ist es kalt.", "Alltag");
        b.Vocab(a2, "der Beruf", "profession", "Nomen", "der", "die Berufe", "Was sind Sie von Beruf?", "Arbeit");
        b.Vocab(a2, "das Geld", "money", "Nomen", "das", null, "Ich habe kein Geld dabei.", "Alltag");
        b.Vocab(a2, "bezahlen", "to pay", "Verb", null, null, "Ich möchte bitte bezahlen.", "Einkaufen");
        b.Vocab(a2, "verstehen", "to understand", "Verb", null, null, "Ich verstehe die Frage nicht.", "Allgemein");
        b.Vocab(a2, "beginnen", "to begin", "Verb", null, null, "Der Kurs beginnt um neun.", "Allgemein");
        b.Vocab(a2, "bekommen", "to get, receive", "Verb", null, null, "Ich bekomme eine E-Mail.", "Allgemein");
        b.Vocab(a2, "vergessen", "to forget", "Verb", null, null, "Vergiss den Termin nicht!", "Allgemein");
        b.Vocab(a2, "einladen", "to invite", "Verb", null, null, "Ich lade dich zum Essen ein.", "Freizeit");

        // ---------------- Practice sets ----------------
        b.Set(a2, "A2 Grammatik-Drill", "Perfekt, Dativ, Wechselpräpositionen, Steigerung & Präteritum.",
            "drill", SkillType.Grammar, false, null,
            l1g_aux, l2g_mc, l2g2_mc, l5g_fb, l6g_fb);

        b.Set(a2, "A2 Modellprüfung (Goethe-Zertifikat A2)",
            "Eine gemischte Modellprüfung über alle Fertigkeiten. Empfohlene Zeit: 90 Minuten.",
            "exam", null, true, 90,
            l1g_aux, l2g_mc, l4g_mc, l5g_fb);
    }

    private static void BuildB1(CurriculumBuilder b)
    {
        var b1 = b.Level(CefrLevel.B1,
            "B1 – Fortgeschrittene Sprachverwendung",
            "Nebensätze, Konjunktiv II, Relativsätze, Adjektivdeklination, Verben mit Präposition und Futur I — Meinungen äußern, Beruf und Umwelt. Ziel: Goethe-Zertifikat B1.",
            "Goethe-Zertifikat B1");

        // ---------------- Unit 1: Nebensätze ----------------
        var u1 = b.Unit(b1, "Nebensätze: weil, dass, wenn, obwohl", "Sätze verbinden und begründen — Verb am Satzende.", "Verbindung");

        var l1g = b.Lesson(u1, "Nebensätze & Konnektoren", SkillType.Grammar,
            "## Nebensätze\nIn Nebensätzen steht das **konjugierte Verb am Ende**.\n\n- **weil** (Grund): Ich lerne Deutsch, **weil** ich in Wien **arbeite**.\n- **dass** (Inhalt): Ich glaube, **dass** er recht **hat**.\n- **wenn** (Bedingung/Zeit): **Wenn** ich Zeit **habe**, lese ich.\n- **obwohl** (Gegensatz): Ich gehe spazieren, **obwohl** es **regnet**.",
            grammarTopic: "Nebensatz", minutes: 22);
        var l1g_mc = b.Ex(l1g, ExerciseType.MultipleChoice, SkillType.Grammar, "Wähle die richtige Wortstellung.",
            new { question = "Ich bleibe heute zu Hause, …", options = new[] { "weil ich bin krank", "weil ich krank bin", "weil bin ich krank", "weil krank ich bin" } },
            new { correctIndex = 1 }, "Im Nebensatz steht das Verb am Ende: »…, weil ich krank **bin**.«", difficulty: 3);
        b.Ex(l1g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze die passende Konjunktion.",
            new { text = "Ich gehe spazieren, ___ es regnet. (Gegensatz)", blanks = new[] { new { hint = "Konjunktion" } } },
            new { answers = new[] { new[] { "obwohl" } } }, "Gegensatz → **obwohl** (Verb am Ende).", difficulty: 3);
        b.Ex(l1g, ExerciseType.Reorder, SkillType.Grammar, "Bilde den Nebensatz.",
            new { tokens = new[] { "dass", "er", "recht", "hat" } },
            new { answer = "dass er recht hat" }, "Nach »dass« steht das Verb am Ende: »dass er recht hat«.", difficulty: 3);

        var l1r = b.Lesson(u1, "Lesen: Warum Deutsch lernen?", SkillType.Reading,
            "## Lies den Text\nViele Menschen lernen Deutsch, weil sie in einem deutschsprachigen Land arbeiten möchten. Andere lernen es, weil sie deutsche Literatur im Original lesen wollen. Obwohl die Grammatik schwierig ist, macht das Lernen vielen Spaß.",
            grammarTopic: "Nebensatz");
        b.Ex(l1r, ExerciseType.ReadingComprehension, SkillType.Reading, "Warum lernen manche Menschen Deutsch?",
            new { question = "Warum lernen manche Menschen Deutsch (laut Text)?", options = new[] { "weil es leicht ist", "weil sie Literatur im Original lesen wollen", "weil es Pflicht ist", "weil es billig ist" } },
            new { correctIndex = 1 }, "»…, weil sie deutsche Literatur im Original lesen wollen.«", difficulty: 2);

        var l1w = b.Lesson(u1, "Schreiben: Deine Meinung", SkillType.Writing,
            "Schreib deine Meinung zum Sprachenlernen — mit Begründungen (weil, dass).", grammarTopic: "Nebensatz");
        b.Ex(l1w, ExerciseType.Writing, SkillType.Writing, "Warum lernst du Deutsch? Begründe (mind. 70 Wörter).",
            new { prompt = "Erkläre deine Gründe mit »weil« und »dass«. Nenne mindestens zwei Argumente.", minWords = 70 },
            new { }, "Nutze Nebensätze: »Ich lerne Deutsch, weil …«, »Ich finde, dass …«.", difficulty: 3);

        // ---------------- Unit 2: Konjunktiv II ----------------
        var u2 = b.Unit(b1, "Konjunktiv II – höflich & hypothetisch", "Höfliche Bitten und irreale Wünsche ausdrücken.", "Höflichkeit");

        var l2g = b.Lesson(u2, "Konjunktiv II: würde, könnte, hätte, wäre", SkillType.Grammar,
            "## Konjunktiv II\nFür höfliche Bitten und Irreales.\n\n- **würde** + Infinitiv: Ich **würde** gern mitkommen.\n- **könnte**: **Könnten** Sie mir helfen?\n- **hätte / wäre**: Wenn ich Zeit **hätte**, **wäre** ich glücklicher.\n\n*Wenn ich reich **wäre**, **würde** ich reisen.*",
            grammarTopic: "Konjunktiv II", minutes: 22);
        var l2g_mc = b.Ex(l2g, ExerciseType.MultipleChoice, SkillType.Grammar, "Höfliche Bitte: »___ Sie mir bitte helfen?«",
            new { question = "___ Sie mir bitte helfen?", options = new[] { "Können", "Könnten", "Konnten", "Kannst" } },
            new { correctIndex = 1 }, "Höflich → Konjunktiv II: **Könnten** Sie …?", difficulty: 3);
        b.Ex(l2g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze den Konjunktiv II (irreal).",
            new { text = "Wenn ich mehr Zeit ___, ___ ich mehr lesen.", blanks = new[] { new { hint = "haben→K2" }, new { hint = "würde" } } },
            new { answers = new[] { new[] { "hätte" }, new[] { "würde" } } }, "»Wenn ich Zeit **hätte**, **würde** ich mehr lesen.«", difficulty: 3);

        var l2s = b.Lesson(u2, "Sprechen: Eine höfliche Bitte", SkillType.Speaking,
            "Formuliere eine höfliche Bitte im Konjunktiv II.", grammarTopic: "Konjunktiv II");
        b.Ex(l2s, ExerciseType.Speaking, SkillType.Speaking, "Sprich: Bitte höflich um Hilfe.",
            new { targetText = "Könnten Sie mir bitte helfen? Ich hätte eine Frage.", prompt = "Höfliche Bitte mit Konjunktiv II." },
            new { }, "Nutze »Könnten Sie …?« und »Ich hätte …«.", difficulty: 3);

        // ---------------- Unit 3: Relativsätze ----------------
        var u3 = b.Unit(b1, "Relativsätze", "Personen und Dinge genauer beschreiben.", "Beschreibung");

        var l3g = b.Lesson(u3, "Relativsätze (Nominativ & Akkusativ)", SkillType.Grammar,
            "## Relativsätze\nDas Relativpronomen richtet sich nach dem Bezugswort.\n\n| | maskulin | feminin | neutrum | Plural |\n|---|---|---|---|---|\n| Nom. | der | die | das | die |\n| Akk. | den | die | das | die |\n\n*Der Mann, **der** dort steht, ist mein Lehrer. Das Buch, **das** ich lese, ist spannend.*",
            grammarTopic: "Relativsatz", minutes: 22);
        var l3g_mc = b.Ex(l3g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Das ist der Mann, ___ in Berlin wohnt.«",
            new { question = "Das ist der Mann, ___ in Berlin wohnt.", options = new[] { "die", "das", "der", "den" } },
            new { correctIndex = 2 }, "Bezugswort »der Mann« (m, Nominativ im Relativsatz: Subjekt) → **der**.", difficulty: 3);
        b.Ex(l3g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze das Relativpronomen.",
            new { text = "Das Buch, ___ ich gerade lese, ist spannend.", blanks = new[] { new { hint = "neutrum, Akk." } } },
            new { answers = new[] { new[] { "das" } } }, "»das Buch« (n), Akkusativobjekt → **das**.", difficulty: 3);

        var l3r = b.Lesson(u3, "Lesen: Eine Person beschreiben", SkillType.Reading,
            "## Lies den Text\nMein Nachbar, der seit zwanzig Jahren hier wohnt, ist Musiker. Die Wohnung, die er gemietet hat, ist sehr groß. Das Instrument, das er am liebsten spielt, ist das Klavier.",
            grammarTopic: "Relativsatz");
        b.Ex(l3r, ExerciseType.ReadingComprehension, SkillType.Reading, "Welches Instrument spielt der Nachbar am liebsten?",
            new { question = "Welches Instrument spielt der Nachbar am liebsten?", options = new[] { "Gitarre", "Klavier", "Geige", "Flöte" } },
            new { correctIndex = 1 }, "»Das Instrument, das er am liebsten spielt, ist das Klavier.«", difficulty: 2);

        // ---------------- Unit 4: Adjektivdeklination ----------------
        var u4 = b.Unit(b1, "Adjektivdeklination", "Adjektivendungen sicher benutzen.", "Grammatik");

        var l4g = b.Lesson(u4, "Adjektivendungen (Nominativ & Akkusativ)", SkillType.Grammar,
            "## Adjektivendungen\nNach **bestimmtem** Artikel (der/die/das) meist **-e / -en**:\n- der **rote** Mantel · die **rote** Jacke · das **rote** Auto\n\nNach **unbestimmtem** Artikel (ein):\n- ein **roter** Mantel (m) · eine **rote** Jacke (f) · ein **rotes** Auto (n)\n- Akkusativ m: einen **roten** Mantel",
            grammarTopic: "Adjektivdeklination", minutes: 22);
        var l4g_mc = b.Ex(l4g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich kaufe einen ___ Mantel.« (rot, m, Akkusativ)",
            new { question = "Ich kaufe einen ___ Mantel.", options = new[] { "rote", "roter", "roten", "rotes" } },
            new { correctIndex = 2 }, "ein + maskulin im Akkusativ → Adjektiv auf **-en**: einen rot**en** Mantel.", difficulty: 4);
        b.Ex(l4g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze die Adjektivendung.",
            new { text = "Das ist ein ___ Auto. (schön, neutrum, Nominativ)", blanks = new[] { new { hint = "-es?" } } },
            new { answers = new[] { new[] { "schönes" } } }, "ein + neutrum Nominativ → **schönes** Auto.", difficulty: 4);

        var l4r = b.Lesson(u4, "Lesen: Eine Wohnungsanzeige", SkillType.Reading,
            "## Lies die Anzeige\nWir vermieten eine helle, moderne Wohnung im Zentrum. Die ruhige Lage und der schöne Balkon sind ideal für eine kleine Familie. Ein neuer Aufzug ist vorhanden.",
            grammarTopic: "Adjektivdeklination");
        b.Ex(l4r, ExerciseType.ReadingComprehension, SkillType.Reading, "Was wird über die Wohnung gesagt?",
            new { question = "Wie wird die Wohnung beschrieben?", options = new[] { "dunkel und alt", "hell und modern", "groß und teuer", "laut und zentral" } },
            new { correctIndex = 1 }, "»…eine helle, moderne Wohnung…«", difficulty: 2);

        // ---------------- Unit 5: Verben mit Präposition & Reflexivverben ----------------
        var u5 = b.Unit(b1, "Verben mit Präposition & Reflexivverben", "Feste Verb-Präposition-Verbindungen benutzen.", "Grammatik");

        var l5g = b.Lesson(u5, "Verben mit Präposition", SkillType.Grammar,
            "## Verben mit fester Präposition\n- sich freuen **auf** (+Akk): Ich freue mich **auf** das Wochenende.\n- warten **auf** (+Akk): Ich warte **auf** den Bus.\n- sich interessieren **für** (+Akk): Sie interessiert sich **für** Kunst.\n- denken **an** (+Akk): Ich denke oft **an** dich.",
            grammarTopic: "Verben mit Präposition", minutes: 20);
        var l5g_mc = b.Ex(l5g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich freue mich ___ das Wochenende.«",
            new { question = "Ich freue mich ___ das Wochenende.", options = new[] { "für", "auf", "an", "über" } },
            new { correctIndex = 1 }, "sich freuen **auf** (+Akkusativ) = sich auf etwas Zukünftiges freuen.", difficulty: 3);
        b.Ex(l5g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze die Präposition.",
            new { text = "Sie interessiert sich ___ Kunst.", blanks = new[] { new { hint = "Präposition" } } },
            new { answers = new[] { new[] { "für" } } }, "sich interessieren **für** (+Akkusativ).", difficulty: 3);

        var l5l = b.Lesson(u5, "Hörverstehen: Pläne", SkillType.Listening,
            "Hör zu und beantworte die Frage.", grammarTopic: "Verben mit Präposition",
            audio: "Ich freue mich schon sehr auf den Urlaub. Wir warten nur noch auf die Bestätigung vom Hotel. Meine Schwester interessiert sich vor allem für die Museen in Rom.");
        b.Ex(l5l, ExerciseType.ListeningComprehension, SkillType.Listening, "Wofür interessiert sich die Schwester?",
            new { audioText = "Ich freue mich schon sehr auf den Urlaub. Wir warten nur noch auf die Bestätigung vom Hotel. Meine Schwester interessiert sich vor allem für die Museen in Rom.",
                  question = "Wofür interessiert sich die Schwester?", options = new[] { "für das Hotel", "für die Museen", "für das Essen", "für den Strand" } },
            new { correctIndex = 1 }, "»…interessiert sich vor allem für die Museen in Rom.«", difficulty: 3);

        // ---------------- Unit 6: Meinung & Umwelt – Futur I ----------------
        var u6 = b.Unit(b1, "Meinung & Umwelt – Futur I", "Über die Zukunft sprechen und argumentieren.", "Umwelt");

        var l6g = b.Lesson(u6, "Futur I: werden + Infinitiv", SkillType.Grammar,
            "## Futur I\n**werden** (konjugiert) + **Infinitiv** (am Ende).\n\n- ich **werde** … machen · du **wirst** · er **wird** · wir **werden**\n\n*Morgen **werde** ich dich **anrufen**. Wir **werden** mehr für die Umwelt **tun**.*",
            grammarTopic: "Futur I", minutes: 20);
        var l6g_mc = b.Ex(l6g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Morgen ___ ich dich anrufen.«",
            new { question = "Morgen ___ ich dich anrufen.", options = new[] { "werde", "wirst", "wird", "bin" } },
            new { correctIndex = 0 }, "Futur I: ich **werde** … anrufen.", difficulty: 3);
        b.Ex(l6g, ExerciseType.Reorder, SkillType.Grammar, "Bilde den Futur-I-Satz.",
            new { tokens = new[] { "werden", "Wir", "mehr Rad", "fahren" } },
            new { answer = "Wir werden mehr Rad fahren" }, "werden Position 2, Infinitiv am Ende: »Wir werden mehr Rad fahren.«", difficulty: 3);

        var l6v = b.Lesson(u6, "Wortschatz: Umwelt", SkillType.Vocabulary,
            "## Umwelt\nder Klimawandel · die Umwelt · der Müll · trennen · die Energie · sparen · nachhaltig · der Verkehr",
            grammarTopic: "Umwelt");
        b.Ex(l6v, ExerciseType.MultipleChoice, SkillType.Vocabulary, "Was bedeutet »nachhaltig«?",
            new { question = "nachhaltig = ?", options = new[] { "sustainable", "expensive", "fast", "modern" } },
            new { correctIndex = 0 }, "nachhaltig = sustainable.", difficulty: 3);

        var l6w = b.Lesson(u6, "Schreiben: Umwelt schützen", SkillType.Writing,
            "Schreib, was man für die Umwelt tun kann (mit Futur I & Meinung).", grammarTopic: "Umwelt");
        b.Ex(l6w, ExerciseType.Writing, SkillType.Writing, "Wie kann man die Umwelt schützen? (mind. 80 Wörter)",
            new { prompt = "Nenne deine Meinung und konkrete Maßnahmen. Nutze »Ich werde …«, »Man sollte …«, »weil …«.", minWords = 80 },
            new { }, "Argumentiere mit Nebensätzen und Futur I.", difficulty: 3);

        // ---------------- Unit 7: Infinitivsätze & Konnektoren ----------------
        var u7 = b.Unit(b1, "Infinitivsätze & Konnektoren", "Infinitiv mit zu, Finalsätze und Hauptsatz-Konnektoren.", "Satzbau");

        var l7g1 = b.Lesson(u7, "Infinitiv mit »zu«", SkillType.Grammar,
            "## Infinitiv mit »zu«\nNach bestimmten Verben und Ausdrücken: »Ich versuche, Deutsch **zu lernen**.«, »Es ist wichtig, früh **aufzustehen**.«\nBei trennbaren Verben steht »zu« **zwischen** Präfix und Stamm: auf**zu**stehen, ein**zu**kaufen.",
            grammarTopic: "Infinitiv mit zu", minutes: 20);
        b.Ex(l7g1, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich habe vor, nächstes Jahr ___.« (umziehen)",
            new { question = "Ich habe vor, nächstes Jahr ___.", options = new[] { "umziehen", "zu umziehen", "umzuziehen", "umziehen zu" } },
            new { correctIndex = 2 }, "trennbares Verb: »zu« kommt in die Mitte → **umzuziehen**.", difficulty: 3);
        b.Ex(l7g1, ExerciseType.Reorder, SkillType.Grammar, "Bilde den Satz.",
            new { tokens = new[] { "versuche", "Ich", "zu", "Deutsch", "lernen" } },
            new { answer = "Ich versuche Deutsch zu lernen" }, "»Ich versuche, Deutsch zu lernen.«", difficulty: 3);

        var l7g2 = b.Lesson(u7, "Finalsätze: um … zu / damit", SkillType.Grammar,
            "## Absicht: um … zu / damit\n- **um … zu** (gleiches Subjekt): Ich lerne, **um** einen Job **zu** finden.\n- **damit** (verschiedene Subjekte): Ich erkläre es, **damit** du es verstehst.",
            grammarTopic: "Finalsatz", minutes: 20);
        b.Ex(l7g2, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich spare Geld, ___ ein Auto zu kaufen.«",
            new { question = "Ich spare Geld, ___ ein Auto zu kaufen.", options = new[] { "damit", "um", "weil", "dass" } },
            new { correctIndex = 1 }, "gleiches Subjekt + »zu« → **um** … zu.", difficulty: 3);
        b.Ex(l7g2, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze (verschiedene Subjekte).",
            new { text = "Ich schreibe es auf, ___ ich es nicht vergesse.", blanks = new[] { new { hint = "verschiedene Subjekte" } } },
            new { answers = new[] { new[] { "damit" } } }, "verschiedene Subjekte → **damit**.", difficulty: 3);

        var l7g3 = b.Lesson(u7, "Konnektoren im Hauptsatz: deshalb, trotzdem", SkillType.Grammar,
            "## Hauptsatz-Konnektoren\nDiese stehen auf **Position 1**, das Verb folgt direkt (Position 2).\n- **deshalb / deswegen** (Folge): Es regnet, **deshalb bleibe** ich zu Hause.\n- **trotzdem** (Gegensatz): Es regnet; **trotzdem gehe** ich raus.",
            grammarTopic: "Konnektoren (Hauptsatz)", minutes: 18);
        b.Ex(l7g3, ExerciseType.MultipleChoice, SkillType.Grammar, "»Ich bin müde, ___ arbeite ich weiter.« (Gegensatz)",
            new { question = "Ich bin müde, ___ arbeite ich weiter.", options = new[] { "deshalb", "trotzdem", "deswegen", "weil" } },
            new { correctIndex = 1 }, "Gegensatz → **trotzdem** (Verb folgt direkt).", difficulty: 3);
        b.Ex(l7g3, ExerciseType.MultipleChoice, SkillType.Grammar, "Was steht direkt nach »deshalb«?",
            new { question = "Nach »deshalb« steht …", options = new[] { "das Subjekt", "das Verb", "ein Komma", "nichts" } },
            new { correctIndex = 1 }, "Position 1 = deshalb, Position 2 = **das Verb**.", difficulty: 3);

        // ---------------- Vocabulary ----------------
        b.Vocab(b1, "der Klimawandel", "climate change", "Nomen", "der", null, "Der Klimawandel ist ein großes Problem.", "Umwelt");
        b.Vocab(b1, "die Umwelt", "environment", "Nomen", "die", null, "Wir müssen die Umwelt schützen.", "Umwelt");
        b.Vocab(b1, "nachhaltig", "sustainable", "Adjektiv", null, null, "Wir leben nachhaltig.", "Umwelt");
        b.Vocab(b1, "die Meinung", "opinion", "Nomen", "die", "die Meinungen", "Meiner Meinung nach ist das falsch.", "Meinung");
        b.Vocab(b1, "der Vorteil", "advantage", "Nomen", "der", "die Vorteile", "Das hat viele Vorteile.", "Meinung");
        b.Vocab(b1, "der Nachteil", "disadvantage", "Nomen", "der", "die Nachteile", "Es gibt auch Nachteile.", "Meinung");
        b.Vocab(b1, "sich bewerben", "to apply (for a job)", "Verb", null, null, "Ich bewerbe mich um die Stelle.", "Beruf");
        b.Vocab(b1, "die Erfahrung", "experience", "Nomen", "die", "die Erfahrungen", "Sie hat viel Erfahrung.", "Beruf");
        b.Vocab(b1, "verbessern", "to improve", "Verb", null, null, "Ich möchte mein Deutsch verbessern.", "Lernen");
        b.Vocab(b1, "trotzdem", "nevertheless", "Adverb", null, null, "Es regnet; trotzdem gehe ich raus.", "Verbindung");
        b.Vocab(b1, "deshalb", "therefore", "Adverb", null, null, "Ich bin müde, deshalb bleibe ich zu Hause.", "Verbindung");
        b.Vocab(b1, "die Lösung", "solution", "Nomen", "die", "die Lösungen", "Wir suchen eine Lösung.", "Meinung");
        b.Vocab(b1, "die Stelle", "position, job", "Nomen", "die", "die Stellen", "Sie sucht eine neue Stelle.", "Beruf");
        b.Vocab(b1, "der Lebenslauf", "CV, résumé", "Nomen", "der", "die Lebensläufe", "Schick mir deinen Lebenslauf.", "Beruf");
        b.Vocab(b1, "das Gehalt", "salary", "Nomen", "das", "die Gehälter", "Das Gehalt ist gut.", "Beruf");
        b.Vocab(b1, "der Kollege", "colleague", "Nomen", "der", "die Kollegen", "Mein Kollege hilft mir.", "Beruf");
        b.Vocab(b1, "die Ausbildung", "training, apprenticeship", "Nomen", "die", "die Ausbildungen", "Sie macht eine Ausbildung.", "Beruf");
        b.Vocab(b1, "das Argument", "argument, point", "Nomen", "das", "die Argumente", "Das ist ein gutes Argument.", "Meinung");
        b.Vocab(b1, "der Standpunkt", "viewpoint", "Nomen", "der", "die Standpunkte", "Ich teile deinen Standpunkt.", "Meinung");
        b.Vocab(b1, "überzeugen", "to convince", "Verb", null, null, "Das Argument überzeugt mich.", "Meinung");
        b.Vocab(b1, "der Müll", "garbage, waste", "Nomen", "der", null, "Wir trennen den Müll.", "Umwelt");
        b.Vocab(b1, "die Energie", "energy", "Nomen", "die", "die Energien", "Wir sparen Energie.", "Umwelt");
        b.Vocab(b1, "sparen", "to save", "Verb", null, null, "Ich spare Strom.", "Umwelt");
        b.Vocab(b1, "schützen", "to protect", "Verb", null, null, "Wir müssen die Natur schützen.", "Umwelt");
        b.Vocab(b1, "der Verkehr", "traffic", "Nomen", "der", null, "Der Verkehr ist stark.", "Umwelt");
        b.Vocab(b1, "die Gesellschaft", "society", "Nomen", "die", "die Gesellschaften", "Die Gesellschaft verändert sich.", "Gesellschaft");
        b.Vocab(b1, "die Entwicklung", "development", "Nomen", "die", "die Entwicklungen", "Eine positive Entwicklung.", "Allgemein");
        b.Vocab(b1, "sich entscheiden", "to decide", "Verb", null, null, "Ich kann mich nicht entscheiden.", "Allgemein");
        b.Vocab(b1, "empfehlen", "to recommend", "Verb", null, null, "Ich empfehle dieses Buch.", "Allgemein");
        b.Vocab(b1, "vermeiden", "to avoid", "Verb", null, null, "Wir sollten Müll vermeiden.", "Umwelt");
        b.Vocab(b1, "der Vorschlag", "suggestion", "Nomen", "der", "die Vorschläge", "Ich habe einen Vorschlag.", "Meinung");
        b.Vocab(b1, "die Gewohnheit", "habit", "Nomen", "die", "die Gewohnheiten", "Das ist eine schlechte Gewohnheit.", "Alltag");
        b.Vocab(b1, "selbstständig", "independent, self-employed", "Adjektiv", null, null, "Sie arbeitet selbstständig.", "Beruf");
        b.Vocab(b1, "notwendig", "necessary", "Adjektiv", null, null, "Das ist nicht notwendig.", "Allgemein");
        b.Vocab(b1, "möglich", "possible", "Adjektiv", null, null, "Ist das möglich?", "Allgemein");

        // ---------------- Practice sets ----------------
        b.Set(b1, "B1 Grammatik-Drill", "Nebensätze, Konjunktiv II, Relativsätze, Adjektivendungen, Präpositionen & Futur I.",
            "drill", SkillType.Grammar, false, null,
            l1g_mc, l2g_mc, l3g_mc, l4g_mc, l5g_mc, l6g_mc);

        b.Set(b1, "B1 Modellprüfung (Goethe-Zertifikat B1)",
            "Gemischte Modellprüfung über alle Fertigkeiten. Empfohlene Zeit: 90 Minuten.",
            "exam", null, true, 90,
            l1g_mc, l3g_mc, l4g_mc, l6g_mc);
    }

    private static void BuildB2(CurriculumBuilder b)
    {
        var b2 = b.Level(CefrLevel.B2,
            "B2 – Selbstständige Sprachverwendung",
            "Passiv, anspruchsvolle Konnektoren, indirekte Rede (Konjunktiv I), Nomen-Verb-Verbindungen, Partizipialattribute sowie Arbeitswelt und Medien. Ziel: Goethe-Zertifikat B2.",
            "Goethe-Zertifikat B2");

        // Unit 1: Passiv
        var u1 = b.Unit(b2, "Das Passiv", "Vorgänge ohne Handelnden ausdrücken.", "Grammatik");
        var l1g = b.Lesson(u1, "Vorgangspassiv & Passiv mit Modalverben", SkillType.Grammar,
            "## Das Passiv\n**werden + Partizip II**.\n\n- Präsens: Das Haus **wird gebaut**.\n- Präteritum: Das Haus **wurde gebaut**.\n- Perfekt: Das Haus **ist gebaut worden**.\n- mit Modalverb: Das Problem **muss gelöst werden**.",
            grammarTopic: "Passiv", minutes: 22);
        var l1g_mc = b.Ex(l1g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Der Brief ___ gestern geschrieben.« (Präteritum Passiv)",
            new { question = "Der Brief ___ gestern geschrieben.", options = new[] { "wird", "wurde", "hat", "ist" } },
            new { correctIndex = 1 }, "Präteritum Passiv: **wurde** + Partizip II.", difficulty: 4);
        b.Ex(l1g, ExerciseType.FillInBlank, SkillType.Grammar, "Passiv mit Modalverb: ergänze.",
            new { text = "Das Problem muss schnell ___ ___.", blanks = new[] { new { hint = "Partizip II" }, new { hint = "werden" } } },
            new { answers = new[] { new[] { "gelöst" }, new[] { "werden" } } }, "Modalverb + Partizip II + werden: »…gelöst werden«.", difficulty: 4);
        b.Ex(l1g, ExerciseType.Reorder, SkillType.Grammar, "Bilde einen Passivsatz.",
            new { tokens = new[] { "wird", "Das Haus", "renoviert", "gerade" } },
            new { answer = "Das Haus wird gerade renoviert" }, "werden Position 2, Partizip II am Ende.", difficulty: 3);

        var l1r = b.Lesson(u1, "Lesen: Wie Schokolade hergestellt wird", SkillType.Reading,
            "## Lies den Text\nZuerst werden die Kakaobohnen geröstet. Danach werden sie gemahlen und mit Zucker gemischt. Schließlich wird die Masse in Formen gegossen und gekühlt. So entsteht die Schokolade, die im Supermarkt verkauft wird.",
            grammarTopic: "Passiv");
        b.Ex(l1r, ExerciseType.ReadingComprehension, SkillType.Reading, "Was passiert zuerst?",
            new { question = "Was passiert mit den Kakaobohnen zuerst?", options = new[] { "Sie werden gemahlen", "Sie werden geröstet", "Sie werden gekühlt", "Sie werden verkauft" } },
            new { correctIndex = 1 }, "»Zuerst werden die Kakaobohnen geröstet.«", difficulty: 3);

        // Unit 2: Konnektoren
        var u2 = b.Unit(b2, "Anspruchsvolle Konnektoren", "Komplexe Beziehungen ausdrücken.", "Verbindung");
        var l2g = b.Lesson(u2, "je…desto, sodass, indem, trotzdem", SkillType.Grammar,
            "## Konnektoren (B2)\n- **je … desto**: **Je** mehr ich übe, **desto** besser werde ich.\n- **sodass** (Folge): Er sprach laut, **sodass** alle ihn hörten.\n- **indem** (Mittel): Man lernt, **indem** man Fehler macht.\n- **trotzdem** (Gegensatz): Es war spät; **trotzdem** arbeitete sie weiter.",
            grammarTopic: "Konnektoren", minutes: 20);
        var l2g_mc = b.Ex(l2g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Je mehr man liest, ___ größer wird der Wortschatz.«",
            new { question = "Je mehr man liest, ___ größer wird der Wortschatz.", options = new[] { "desto", "sodass", "indem", "trotzdem" } },
            new { correctIndex = 0 }, "je … **desto** (Komparativ + Komparativ).", difficulty: 4);
        b.Ex(l2g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze den Konnektor (Mittel/Methode).",
            new { text = "Man verbessert seine Aussprache, ___ man laut liest.", blanks = new[] { new { hint = "Mittel" } } },
            new { answers = new[] { new[] { "indem" } } }, "Mittel/Methode → **indem** (Verb am Ende).", difficulty: 4);

        var l2w = b.Lesson(u2, "Schreiben: Vor- und Nachteile", SkillType.Writing,
            "Erörtere ein Thema mit Konnektoren.", grammarTopic: "Konnektoren");
        b.Ex(l2w, ExerciseType.Writing, SkillType.Writing, "Homeoffice: Vor- und Nachteile (mind. 100 Wörter).",
            new { prompt = "Diskutiere Vor- und Nachteile des Homeoffice. Nutze Konnektoren wie »einerseits/andererseits«, »sodass«, »trotzdem«.", minWords = 100 },
            new { }, "Strukturiere mit Konnektoren und einer klaren Schlussfolgerung.", difficulty: 4);

        // Unit 3: Indirekte Rede (Konjunktiv I)
        var u3 = b.Unit(b2, "Indirekte Rede (Konjunktiv I)", "Aussagen anderer wiedergeben.", "Stil");
        var l3g = b.Lesson(u3, "Konjunktiv I", SkillType.Grammar,
            "## Indirekte Rede\nKonjunktiv I gibt Aussagen wieder.\n- sein → er **sei** · haben → er **habe** · kommen → er **komme**\n\n*Direkt: »Ich bin krank.« → Indirekt: Er sagt, er **sei** krank.*",
            grammarTopic: "Konjunktiv I", minutes: 22);
        var l3g_mc = b.Ex(l3g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Er sagt, er ___ krank.« (Konjunktiv I)",
            new { question = "Er sagt, er ___ krank.", options = new[] { "ist", "sei", "wäre", "war" } },
            new { correctIndex = 1 }, "Indirekte Rede → Konjunktiv I: er **sei**.", difficulty: 4);
        b.Ex(l3g, ExerciseType.FillInBlank, SkillType.Grammar, "Setze in indirekte Rede (Konjunktiv I).",
            new { text = "Sie sagte, sie ___ keine Zeit. (haben)", blanks = new[] { new { hint = "K I" } } },
            new { answers = new[] { new[] { "habe" } } }, "haben → Konjunktiv I: sie **habe**.", difficulty: 4);

        var l3r = b.Lesson(u3, "Lesen: Eine Nachricht", SkillType.Reading,
            "## Lies den Text\nDer Minister erklärte, die Wirtschaft entwickle sich positiv. Man habe viele neue Arbeitsplätze geschaffen und werde weiter investieren. Die Opposition kritisierte, das sei nicht genug.",
            grammarTopic: "Konjunktiv I");
        b.Ex(l3r, ExerciseType.ReadingComprehension, SkillType.Reading, "Was sagt der Minister über die Wirtschaft?",
            new { question = "Was sagt der Minister?", options = new[] { "Sie entwickle sich positiv", "Sie sei in der Krise", "Man werde Stellen abbauen", "Es gebe keine Veränderung" } },
            new { correctIndex = 0 }, "»…die Wirtschaft entwickle sich positiv.«", difficulty: 3);

        // Unit 4: Nomen-Verb-Verbindungen
        var u4 = b.Unit(b2, "Nomen-Verb-Verbindungen", "Feste Wendungen der Schriftsprache.", "Stil");
        var l4g = b.Lesson(u4, "Funktionsverbgefüge (Einführung)", SkillType.Grammar,
            "## Nomen-Verb-Verbindungen\n- eine Entscheidung **treffen**\n- in Frage **stellen**\n- zur Verfügung **stehen/stellen**\n- Kritik **üben**\n- eine Rolle **spielen**",
            grammarTopic: "Nomen-Verb-Verbindung", minutes: 20);
        var l4g_mc = b.Ex(l4g, ExerciseType.MultipleChoice, SkillType.Grammar, "Welches Verb passt: »eine Entscheidung ___«?",
            new { question = "eine Entscheidung ___", options = new[] { "machen", "treffen", "nehmen", "geben" } },
            new { correctIndex = 1 }, "Kollokation: eine Entscheidung **treffen**.", difficulty: 4);
        b.Ex(l4g, ExerciseType.Matching, SkillType.Vocabulary, "Ordne Nomen und Verb zu.",
            new { left = new[] { "Kritik", "eine Rolle", "zur Verfügung", "in Frage" }, right = new[] { "spielen", "stellen", "üben", "stehen" } },
            new { pairs = new[] { new[] { 0, 2 }, new[] { 1, 0 }, new[] { 2, 3 }, new[] { 3, 1 } } },
            "Kritik üben, eine Rolle spielen, zur Verfügung stehen, in Frage stellen.", difficulty: 4);

        // Unit 5: Partizipien als Attribut
        var u5 = b.Unit(b2, "Partizipien als Attribut", "Partizip I und II als Adjektive.", "Grammatik");
        var l5g = b.Lesson(u5, "Partizip I & II als Adjektiv", SkillType.Grammar,
            "## Partizipien als Attribut\n- **Partizip I** (-end, gleichzeitig/aktiv): die **lachenden** Kinder\n- **Partizip II** (passiv/abgeschlossen): das **reparierte** Auto",
            grammarTopic: "Partizipialattribut", minutes: 20);
        var l5g_mc = b.Ex(l5g, ExerciseType.MultipleChoice, SkillType.Grammar, "»das ___ Auto« (reparieren, abgeschlossen)",
            new { question = "das ___ Auto (reparieren, abgeschlossen)", options = new[] { "reparierende", "reparierte", "reparieren", "zu reparierende" } },
            new { correctIndex = 1 }, "Abgeschlossen/passiv → Partizip II: das **reparierte** Auto.", difficulty: 4);
        b.Ex(l5g, ExerciseType.FillInBlank, SkillType.Grammar, "Partizip I als Attribut.",
            new { text = "die ___ Sonne (scheinen, gerade jetzt)", blanks = new[] { new { hint = "Partizip I" } } },
            new { answers = new[] { new[] { "scheinende" } } }, "Partizip I: scheinen → **scheinende** Sonne.", difficulty: 4);

        // Unit 6: Arbeitswelt & Medien
        var u6 = b.Unit(b2, "Arbeitswelt & Medien", "Über Beruf und Medien diskutieren.", "Arbeit");
        var l6v = b.Lesson(u6, "Wortschatz: Arbeit & Medien", SkillType.Vocabulary,
            "## Arbeit & Medien\nder Arbeitgeber · der Arbeitnehmer · die Bewerbung · das Vorstellungsgespräch · die Nachricht · die Quelle · zuverlässig · die Schlagzeile",
            grammarTopic: "Arbeit");
        b.Ex(l6v, ExerciseType.MultipleChoice, SkillType.Vocabulary, "Was ist ein »Arbeitnehmer«?",
            new { question = "Arbeitnehmer = ?", options = new[] { "employer", "employee", "colleague", "manager" } },
            new { correctIndex = 1 }, "Arbeitnehmer = employee (Arbeitgeber = employer).", difficulty: 3);
        var l6w = b.Lesson(u6, "Schreiben: Stellungnahme", SkillType.Writing,
            "Nimm Stellung zu Social Media.", grammarTopic: "Medien");
        b.Ex(l6w, ExerciseType.Writing, SkillType.Writing, "Soziale Medien – Fluch oder Segen? (mind. 120 Wörter)",
            new { prompt = "Schreibe eine strukturierte Stellungnahme mit Einleitung, Argumenten (pro/contra) und Fazit.", minWords = 120 },
            new { }, "Argumentiere im Passiv und mit B2-Konnektoren; ziehe ein klares Fazit.", difficulty: 4);

        var l6l = b.Lesson(u6, "Hörverstehen: Im Berufsleben", SkillType.Listening,
            "Hör das Telefongespräch und beantworte die Frage. (Tippe auf 🔊.)", grammarTopic: "Arbeit",
            audio: "Guten Tag, ich rufe wegen der Stelle als Projektmanager an. Könnten Sie mir sagen, ob die Stelle noch frei ist? – Ja, gern. Schicken Sie uns bitte Ihre Bewerbung bis Freitag.");
        b.Ex(l6l, ExerciseType.ListeningComprehension, SkillType.Listening, "Worum geht es im Gespräch?",
            new { audioText = "Guten Tag, ich rufe wegen der Stelle als Projektmanager an. Könnten Sie mir sagen, ob die Stelle noch frei ist? – Ja, gern. Schicken Sie uns bitte Ihre Bewerbung bis Freitag.",
                  question = "Worum geht es im Gespräch?", options = new[] { "um einen Urlaub", "um eine Stelle als Projektmanager", "um eine Rechnung", "um ein Produkt" } },
            new { correctIndex = 1 }, "»Ich rufe wegen der Stelle als Projektmanager an.«", difficulty: 4);

        var l6s = b.Lesson(u6, "Sprechen: Deine Meinung äußern", SkillType.Speaking,
            "Äußere deine Meinung zu einem aktuellen Thema in 2–3 Sätzen. Nimm dich auf.", grammarTopic: "Medien");
        b.Ex(l6s, ExerciseType.Speaking, SkillType.Speaking, "Sprich: Äußere deine Meinung zu sozialen Medien.",
            new { targetText = "Meiner Meinung nach haben soziale Medien sowohl Vorteile als auch Nachteile.", prompt = "Deine Meinung zu sozialen Medien." },
            new { }, "Nutze »Meiner Meinung nach …«, »Ich bin der Ansicht, dass …«, »einerseits … andererseits«.", difficulty: 4);

        // Vocabulary
        b.Vocab(b2, "der Arbeitgeber", "employer", "Nomen", "der", "die Arbeitgeber", "Mein Arbeitgeber ist fair.", "Arbeit");
        b.Vocab(b2, "die Bewerbung", "application", "Nomen", "die", "die Bewerbungen", "Ich schreibe eine Bewerbung.", "Arbeit");
        b.Vocab(b2, "zuverlässig", "reliable", "Adjektiv", null, null, "Er ist sehr zuverlässig.", "Arbeit");
        b.Vocab(b2, "die Quelle", "source", "Nomen", "die", "die Quellen", "Prüfe immer die Quelle.", "Medien");
        b.Vocab(b2, "die Auswirkung", "effect, impact", "Nomen", "die", "die Auswirkungen", "Das hat große Auswirkungen.", "Allgemein");
        b.Vocab(b2, "berücksichtigen", "to take into account", "Verb", null, null, "Wir müssen das berücksichtigen.", "Allgemein");
        b.Vocab(b2, "die Voraussetzung", "prerequisite", "Nomen", "die", "die Voraussetzungen", "Erfahrung ist eine Voraussetzung.", "Arbeit");
        b.Vocab(b2, "zunehmen", "to increase", "Verb", null, null, "Die Zahl nimmt zu.", "Allgemein");
        b.Vocab(b2, "die Herausforderung", "challenge", "Nomen", "die", "die Herausforderungen", "Das ist eine große Herausforderung.", "Allgemein");
        b.Vocab(b2, "sich auseinandersetzen", "to deal with", "Verb", null, null, "Ich setze mich mit dem Thema auseinander.", "Allgemein");
        b.Vocab(b2, "der Zusammenhang", "context, connection", "Nomen", "der", "die Zusammenhänge", "In diesem Zusammenhang …", "Allgemein");
        b.Vocab(b2, "voraussichtlich", "probably, expected", "Adverb", null, null, "Es wird voraussichtlich regnen.", "Allgemein");
        b.Vocab(b2, "die Stellungnahme", "statement, position", "Nomen", "die", "die Stellungnahmen", "Er gab eine Stellungnahme ab.", "Medien");
        b.Vocab(b2, "die Werbung", "advertising", "Nomen", "die", "die Werbungen", "Die Werbung nervt.", "Medien");
        b.Vocab(b2, "der Datenschutz", "data protection", "Nomen", "der", null, "Datenschutz ist wichtig.", "Medien");
        b.Vocab(b2, "die Debatte", "debate", "Nomen", "die", "die Debatten", "Eine hitzige Debatte.", "Medien");
        b.Vocab(b2, "die Maßnahme", "measure", "Nomen", "die", "die Maßnahmen", "Wir ergreifen Maßnahmen.", "Allgemein");
        b.Vocab(b2, "die Folge", "consequence", "Nomen", "die", "die Folgen", "Das hat ernste Folgen.", "Allgemein");
        b.Vocab(b2, "beeinflussen", "to influence", "Verb", null, null, "Das Wetter beeinflusst die Stimmung.", "Allgemein");
        b.Vocab(b2, "ermöglichen", "to enable", "Verb", null, null, "Das Stipendium ermöglicht das Studium.", "Allgemein");
        b.Vocab(b2, "verursachen", "to cause", "Verb", null, null, "Rauchen verursacht Krankheiten.", "Allgemein");
        b.Vocab(b2, "fördern", "to promote, support", "Verb", null, null, "Der Staat fördert die Forschung.", "Allgemein");
        b.Vocab(b2, "der Anstieg", "increase", "Nomen", "der", "die Anstiege", "ein starker Anstieg der Preise", "Allgemein");
        b.Vocab(b2, "der Rückgang", "decline", "Nomen", "der", "die Rückgänge", "ein Rückgang der Arbeitslosigkeit", "Allgemein");
        b.Vocab(b2, "erheblich", "considerable", "Adjektiv", null, null, "ein erheblicher Unterschied", "Allgemein");
        b.Vocab(b2, "vielfältig", "diverse, varied", "Adjektiv", null, null, "Die Aufgaben sind vielfältig.", "Allgemein");
        b.Vocab(b2, "die Fähigkeit", "ability, skill", "Nomen", "die", "die Fähigkeiten", "Sie hat besondere Fähigkeiten.", "Arbeit");
        b.Vocab(b2, "die Verantwortung", "responsibility", "Nomen", "die", null, "Er trägt die Verantwortung.", "Arbeit");
        b.Vocab(b2, "der Fortschritt", "progress", "Nomen", "der", "die Fortschritte", "Du machst große Fortschritte.", "Allgemein");
        b.Vocab(b2, "die Wirkung", "effect", "Nomen", "die", "die Wirkungen", "Das Medikament zeigt Wirkung.", "Allgemein");
        b.Vocab(b2, "zuständig", "responsible, in charge", "Adjektiv", null, null, "Wer ist hier zuständig?", "Arbeit");
        b.Vocab(b2, "umfassend", "comprehensive", "Adjektiv", null, null, "eine umfassende Analyse", "Allgemein");

        b.Set(b2, "B2 Grammatik-Drill", "Passiv, Konnektoren, Konjunktiv I, Nomen-Verb-Verbindungen & Partizipien.",
            "drill", SkillType.Grammar, false, null,
            l1g_mc, l2g_mc, l3g_mc, l4g_mc, l5g_mc);
        b.Set(b2, "B2 Modellprüfung (Goethe-Zertifikat B2)",
            "Gemischte Modellprüfung über alle Fertigkeiten. Empfohlene Zeit: 90 Minuten.",
            "exam", null, true, 90,
            l1g_mc, l2g_mc, l3g_mc, l5g_mc);
    }

    private static void BuildC1(CurriculumBuilder b)
    {
        var c1 = b.Level(CefrLevel.C1,
            "C1 – Fachkundige Sprachkenntnisse",
            "Nominalstil, erweiterte Partizipialattribute, Konnektoren des gehobenen Stils, Funktionsverbgefüge, anspruchsvoller Konjunktiv sowie Wissenschaft und Gesellschaft. Ziel: Goethe-Zertifikat C1.",
            "Goethe-Zertifikat C1");

        // Unit 1: Nominalstil
        var u1 = b.Unit(c1, "Nominalstil", "Verbalstil in Nominalstil umwandeln.", "Stil");
        var l1g = b.Lesson(u1, "Verbalstil ↔ Nominalstil", SkillType.Grammar,
            "## Nominalstil\nTypisch für Wissenschaft und Verwaltung.\n\n- Verbalstil: »**Weil die Preise steigen**, …«\n- Nominalstil: »**Aufgrund des Anstiegs der Preise** …« / »**Aufgrund steigender Preise** …«\n- »**Nachdem** er angekommen **war**« → »**Nach seiner Ankunft**«",
            grammarTopic: "Nominalisierung", minutes: 24);
        var l1g_mc = b.Ex(l1g, ExerciseType.MultipleChoice, SkillType.Grammar, "Nominalisiere: »weil die Preise steigen«",
            new { question = "Nominalstil von »weil die Preise steigen«:", options = new[] { "wegen die Preise steigen", "aufgrund steigender Preise", "weil der Preisanstieg", "trotz der Preise" } },
            new { correctIndex = 1 }, "Nominalstil: **aufgrund steigender Preise** (Genitiv + Partizip).", difficulty: 5);
        b.Ex(l1g, ExerciseType.MultipleChoice, SkillType.Grammar, "Nominalisiere: »nachdem er angekommen war«",
            new { question = "Nominalstil von »nachdem er angekommen war«:", options = new[] { "nach seiner Ankunft", "bei seine Ankunft", "während er ankam", "vor der Ankunft" } },
            new { correctIndex = 0 }, "temporal »nachdem« → »**nach** seiner **Ankunft**«.", difficulty: 5);

        var l1r = b.Lesson(u1, "Lesen: Wissenschaftssprache", SkillType.Reading,
            "## Lies den Text\nDie Untersuchung der Daten erfolgte unter Berücksichtigung mehrerer Faktoren. Aufgrund der Komplexität des Themas war eine eindeutige Schlussfolgerung zunächst nicht möglich. Erst nach Auswertung aller Quellen ließ sich ein Trend erkennen.",
            grammarTopic: "Nominalisierung");
        b.Ex(l1r, ExerciseType.ReadingComprehension, SkillType.Reading, "Wann ließ sich ein Trend erkennen?",
            new { question = "Wann ließ sich ein Trend erkennen?", options = new[] { "sofort", "nach Auswertung aller Quellen", "gar nicht", "vor der Untersuchung" } },
            new { correctIndex = 1 }, "»Erst nach Auswertung aller Quellen ließ sich ein Trend erkennen.«", difficulty: 4);

        // Unit 2: Erweiterte Partizipialattribute
        var u2 = b.Unit(c1, "Erweiterte Partizipialattribute", "Komplexe Attribute verstehen und bilden.", "Grammatik");
        var l2g = b.Lesson(u2, "Erweiterte Partizipialkonstruktionen", SkillType.Grammar,
            "## Erweiterte Attribute\n»die **in den letzten Jahren stark gestiegenen** Preise« = »die Preise, die in den letzten Jahren stark gestiegen sind«.\n\n- Partizip I (aktiv/gleichzeitig): »das **schnell wachsende** Unternehmen«\n- Partizip II (passiv/vollendet): »der **gestern veröffentlichte** Bericht«",
            grammarTopic: "Partizipialattribut", minutes: 24);
        var l2g_mc = b.Ex(l2g, ExerciseType.MultipleChoice, SkillType.Grammar, "»der gestern ___ Bericht« (veröffentlichen)",
            new { question = "der gestern ___ Bericht", options = new[] { "veröffentlichende", "veröffentlichte", "veröffentlichen", "zu veröffentlichende" } },
            new { correctIndex = 1 }, "passiv & vollendet → Partizip II: der **veröffentlichte** Bericht.", difficulty: 5);
        b.Ex(l2g, ExerciseType.MultipleChoice, SkillType.Grammar, "»das schnell ___ Unternehmen« (wachsen, gerade)",
            new { question = "das schnell ___ Unternehmen", options = new[] { "gewachsene", "wachsende", "gewachsen", "zu wachsende" } },
            new { correctIndex = 1 }, "aktiv & gleichzeitig → Partizip I: das **wachsende** Unternehmen.", difficulty: 5);

        var l2r = b.Lesson(u2, "Lesen: Ein Fachartikel", SkillType.Reading,
            "## Lies den Text\nDer von zahlreichen Experten kritisierte Vorschlag wurde überarbeitet. Die nun vorliegende, deutlich verbesserte Fassung berücksichtigt die zuvor geäußerten Bedenken und stellt einen tragfähigen Kompromiss dar.",
            grammarTopic: "Partizipialattribut");
        b.Ex(l2r, ExerciseType.ReadingComprehension, SkillType.Reading, "Wie wird die neue Fassung beschrieben?",
            new { question = "Wie ist die neue Fassung?", options = new[] { "unverändert", "deutlich verbessert", "schlechter", "unvollständig" } },
            new { correctIndex = 1 }, "»Die nun vorliegende, deutlich verbesserte Fassung …«", difficulty: 4);

        // Unit 3: Konnektoren des gehobenen Stils
        var u3 = b.Unit(c1, "Konnektoren des gehobenen Stils", "Präzise und formell verknüpfen.", "Stil");
        var l3g = b.Lesson(u3, "folglich, dennoch, zumal, hingegen", SkillType.Grammar,
            "## Gehobene Konnektoren\n- **folglich / infolgedessen** (Folge): …, **folglich** muss gehandelt werden.\n- **dennoch / gleichwohl** (Gegensatz): Es war riskant; **dennoch** wagte er es.\n- **zumal** (verstärkender Grund): Wir bleiben, **zumal** es regnet.\n- **hingegen** (Kontrast): Er ist optimistisch, sie **hingegen** skeptisch.",
            grammarTopic: "Konnektoren (C1)", minutes: 22);
        var l3g_mc = b.Ex(l3g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Das Experiment war riskant; ___ wurde es durchgeführt.« (Gegensatz)",
            new { question = "Das Experiment war riskant; ___ wurde es durchgeführt.", options = new[] { "folglich", "dennoch", "zumal", "deshalb" } },
            new { correctIndex = 1 }, "Gegensatz im gehobenen Stil → **dennoch** (auch: gleichwohl).", difficulty: 5);
        b.Ex(l3g, ExerciseType.FillInBlank, SkillType.Grammar, "Ergänze den Konnektor (Folge, formell).",
            new { text = "Die Nachfrage ist gesunken; ___ müssen die Preise angepasst werden.", blanks = new[] { new { hint = "Folge" } } },
            new { answers = new[] { new[] { "folglich" } } }, "Folge (formell) → **folglich** (auch: infolgedessen).", difficulty: 5);

        // Unit 4: Funktionsverbgefüge
        var u4 = b.Unit(c1, "Funktionsverbgefüge", "Idiomatische Verb-Nomen-Gefüge.", "Stil");
        var l4g = b.Lesson(u4, "Funktionsverbgefüge", SkillType.Grammar,
            "## Funktionsverbgefüge\n- in Frage **stellen** (= bezweifeln)\n- zur Verfügung **stehen/stellen**\n- in Betracht **ziehen** (= erwägen)\n- Anwendung **finden** (= angewendet werden)\n- in Kraft **treten** (= gültig werden)",
            grammarTopic: "Funktionsverbgefüge", minutes: 22);
        var l4g_mc = b.Ex(l4g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Diese Möglichkeit sollten wir in Betracht ___.«",
            new { question = "Diese Möglichkeit sollten wir in Betracht ___.", options = new[] { "nehmen", "ziehen", "stellen", "bringen" } },
            new { correctIndex = 1 }, "Funktionsverbgefüge: in Betracht **ziehen** (= erwägen).", difficulty: 5);
        b.Ex(l4g, ExerciseType.Matching, SkillType.Vocabulary, "Ordne die Funktionsverbgefüge zu.",
            new { left = new[] { "in Frage", "zur Verfügung", "in Kraft", "Anwendung" }, right = new[] { "treten", "finden", "stellen", "stehen" } },
            new { pairs = new[] { new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2, 0 }, new[] { 3, 1 } } },
            "in Frage stellen, zur Verfügung stehen, in Kraft treten, Anwendung finden.", difficulty: 5);

        // Unit 5: Konjunktiv & Distanz
        var u5 = b.Unit(c1, "Konjunktiv & Distanz", "Irreales und Distanz ausdrücken.", "Stil");
        var l5g = b.Lesson(u5, "Konjunktiv II (Vergangenheit) & Distanz", SkillType.Grammar,
            "## Konjunktiv II der Vergangenheit\n**hätte/wäre + Partizip II**: »Wenn ich das **gewusst hätte**, **wäre** ich nicht **gekommen**.«\n\nAuch für vorsichtige Aussagen: »Das **dürfte** schwierig sein.«, »Man **könnte** argumentieren, dass …«",
            grammarTopic: "Konjunktiv II", minutes: 22);
        var l5g_mc = b.Ex(l5g, ExerciseType.MultipleChoice, SkillType.Grammar, "»Wenn ich das gewusst hätte, ___ ich anders entschieden.«",
            new { question = "Wenn ich das gewusst hätte, ___ ich anders entschieden.", options = new[] { "würde", "hätte", "wäre", "habe" } },
            new { correctIndex = 1 }, "Irreal Vergangenheit: »…, **hätte** ich anders entschieden.«", difficulty: 5);
        b.Ex(l5g, ExerciseType.FillInBlank, SkillType.Grammar, "Vorsichtige Aussage (Konjunktiv II).",
            new { text = "Das ___ schwierig sein. (dürfen, Vermutung)", blanks = new[] { new { hint = "K II" } } },
            new { answers = new[] { new[] { "dürfte" } } }, "Vermutung → **dürfte** (Konjunktiv II von »dürfen«).", difficulty: 5);

        // Unit 6: Wissenschaft & Gesellschaft
        var u6 = b.Unit(c1, "Wissenschaft & Gesellschaft", "Abstrakte Themen diskutieren.", "Gesellschaft");
        var l6v = b.Lesson(u6, "Wortschatz: abstrakte Begriffe", SkillType.Vocabulary,
            "## Abstrakte Begriffe\ndie Hypothese · die Erkenntnis · der Aspekt · die Entwicklung · die Tendenz · maßgeblich · die Voraussetzung · der Diskurs",
            grammarTopic: "Wissenschaft");
        b.Ex(l6v, ExerciseType.MultipleChoice, SkillType.Vocabulary, "Was bedeutet »maßgeblich«?",
            new { question = "maßgeblich = ?", options = new[] { "decisive, significant", "unimportant", "temporary", "visible" } },
            new { correctIndex = 0 }, "maßgeblich = decisive/significant.", difficulty: 4);
        var l6w = b.Lesson(u6, "Schreiben: Wissenschaftliche Erörterung", SkillType.Writing,
            "Verfasse eine argumentative Erörterung.", grammarTopic: "Gesellschaft");
        b.Ex(l6w, ExerciseType.Writing, SkillType.Writing, "»Künstliche Intelligenz: Chance oder Gefahr?« – Erörtere (mind. 150 Wörter).",
            new { prompt = "Schreibe eine differenzierte Erörterung mit These, Argumenten, Gegenargumenten und begründetem Fazit. Nutze Nominalstil und gehobene Konnektoren.", minWords = 150 },
            new { }, "Argumentiere abstrakt und kohärent; nutze Funktionsverbgefüge und C1-Konnektoren.", difficulty: 5);

        var l6l = b.Lesson(u6, "Hörverstehen: Ein Vortrag", SkillType.Listening,
            "Hör den Ausschnitt aus einem Vortrag und beantworte die Frage.", grammarTopic: "Wissenschaft",
            audio: "Die vorliegende Studie kommt zu dem Ergebnis, dass der Klimawandel die Wirtschaft erheblich beeinflusst. Allerdings sind weitere Untersuchungen notwendig, um die langfristigen Folgen zuverlässig abzuschätzen.");
        b.Ex(l6l, ExerciseType.ListeningComprehension, SkillType.Listening, "Was ist das Ergebnis der Studie?",
            new { audioText = "Die vorliegende Studie kommt zu dem Ergebnis, dass der Klimawandel die Wirtschaft erheblich beeinflusst. Allerdings sind weitere Untersuchungen notwendig, um die langfristigen Folgen zuverlässig abzuschätzen.",
                  question = "Was ist das Ergebnis der Studie?", options = new[] { "Der Klimawandel hat keine Folgen", "Der Klimawandel beeinflusst die Wirtschaft erheblich", "Die Studie ist abgeschlossen", "Die Wirtschaft wächst" } },
            new { correctIndex = 1 }, "»… dass der Klimawandel die Wirtschaft erheblich beeinflusst.«", difficulty: 5);

        var l6s = b.Lesson(u6, "Sprechen: Ein Argument vortragen", SkillType.Speaking,
            "Trage ein Argument zu einem abstrakten Thema strukturiert vor. Nimm dich auf.", grammarTopic: "Gesellschaft");
        b.Ex(l6s, ExerciseType.Speaking, SkillType.Speaking, "Sprich: Begründe, warum lebenslanges Lernen wichtig ist.",
            new { targetText = "Lebenslanges Lernen ist von zentraler Bedeutung, da sich die Arbeitswelt rasant verändert.", prompt = "Argumentiere für lebenslanges Lernen." },
            new { }, "Strukturiere: These – Begründung – Beispiel – Schlussfolgerung. Nutze gehobene Konnektoren.", difficulty: 5);

        // Vocabulary
        b.Vocab(c1, "die Hypothese", "hypothesis", "Nomen", "die", "die Hypothesen", "Die Hypothese wurde bestätigt.", "Wissenschaft");
        b.Vocab(c1, "die Erkenntnis", "insight, finding", "Nomen", "die", "die Erkenntnisse", "Eine wichtige Erkenntnis.", "Wissenschaft");
        b.Vocab(c1, "maßgeblich", "decisive, significant", "Adjektiv", null, null, "Er war maßgeblich beteiligt.", "Wissenschaft");
        b.Vocab(c1, "die Tendenz", "tendency, trend", "Nomen", "die", "die Tendenzen", "Es gibt eine klare Tendenz.", "Wissenschaft");
        b.Vocab(c1, "der Diskurs", "discourse", "Nomen", "der", "die Diskurse", "der öffentliche Diskurs", "Gesellschaft");
        b.Vocab(c1, "nachvollziehbar", "comprehensible", "Adjektiv", null, null, "Sein Argument ist nachvollziehbar.", "Gesellschaft");
        b.Vocab(c1, "die Differenzierung", "differentiation", "Nomen", "die", "die Differenzierungen", "Eine Differenzierung ist nötig.", "Wissenschaft");
        b.Vocab(c1, "wesentlich", "essential", "Adjektiv", null, null, "Das ist ein wesentlicher Punkt.", "Allgemein");
        b.Vocab(c1, "die Folgerung", "conclusion", "Nomen", "die", "die Folgerungen", "Daraus ergibt sich die Folgerung …", "Wissenschaft");
        b.Vocab(c1, "umstritten", "controversial", "Adjektiv", null, null, "Das Thema ist umstritten.", "Gesellschaft");
        b.Vocab(c1, "gewährleisten", "to ensure, guarantee", "Verb", null, null, "Wir müssen Qualität gewährleisten.", "Allgemein");
        b.Vocab(c1, "der Aspekt", "aspect", "Nomen", "der", "die Aspekte", "Ein wichtiger Aspekt ist …", "Wissenschaft");
        b.Vocab(c1, "die Auseinandersetzung", "debate, examination", "Nomen", "die", "die Auseinandersetzungen", "die kritische Auseinandersetzung mit dem Thema", "Gesellschaft");
        b.Vocab(c1, "die Grundlage", "basis, foundation", "Nomen", "die", "die Grundlagen", "auf wissenschaftlicher Grundlage", "Wissenschaft");
        b.Vocab(c1, "der Beleg", "evidence, proof", "Nomen", "der", "die Belege", "Es gibt klare Belege dafür.", "Wissenschaft");
        b.Vocab(c1, "die These", "thesis, claim", "Nomen", "die", "die Thesen", "Er vertritt die These, dass …", "Wissenschaft");
        b.Vocab(c1, "das Phänomen", "phenomenon", "Nomen", "das", "die Phänomene", "ein gesellschaftliches Phänomen", "Wissenschaft");
        b.Vocab(c1, "die Wahrnehmung", "perception", "Nomen", "die", "die Wahrnehmungen", "die subjektive Wahrnehmung", "Wissenschaft");
        b.Vocab(c1, "hinterfragen", "to question critically", "Verb", null, null, "Man sollte alles kritisch hinterfragen.", "Stil");
        b.Vocab(c1, "verdeutlichen", "to clarify, illustrate", "Verb", null, null, "Ein Beispiel verdeutlicht das Problem.", "Stil");
        b.Vocab(c1, "beruhen", "to be based on", "Verb", null, null, "Die Theorie beruht auf Daten.", "Stil");
        b.Vocab(c1, "ausschlaggebend", "decisive", "Adjektiv", null, null, "Der Preis war ausschlaggebend.", "Wissenschaft");
        b.Vocab(c1, "weitreichend", "far-reaching", "Adjektiv", null, null, "weitreichende Konsequenzen", "Allgemein");
        b.Vocab(c1, "die Komplexität", "complexity", "Nomen", "die", null, "die Komplexität des Themas", "Wissenschaft");
        b.Vocab(c1, "das Ausmaß", "extent, scale", "Nomen", "das", "die Ausmaße", "das Ausmaß der Schäden", "Wissenschaft");
        b.Vocab(c1, "die Annahme", "assumption", "Nomen", "die", "die Annahmen", "Diese Annahme ist falsch.", "Wissenschaft");
        b.Vocab(c1, "die Konsequenz", "consequence", "Nomen", "die", "die Konsequenzen", "Das hat Konsequenzen.", "Allgemein");
        b.Vocab(c1, "gesellschaftlich", "societal", "Adjektiv", null, null, "gesellschaftliche Veränderungen", "Gesellschaft");
        b.Vocab(c1, "fragwürdig", "questionable, dubious", "Adjektiv", null, null, "eine fragwürdige Methode", "Gesellschaft");
        b.Vocab(c1, "der Widerspruch", "contradiction", "Nomen", "der", "die Widersprüche", "ein innerer Widerspruch", "Wissenschaft");
        b.Vocab(c1, "veranschaulichen", "to illustrate", "Verb", null, null, "Die Grafik veranschaulicht den Trend.", "Stil");

        b.Set(c1, "C1 Grammatik-Drill", "Nominalstil, Partizipialattribute, gehobene Konnektoren, Funktionsverbgefüge & Konjunktiv.",
            "drill", SkillType.Grammar, false, null,
            l1g_mc, l2g_mc, l3g_mc, l4g_mc, l5g_mc);
        b.Set(c1, "C1 Modellprüfung (Goethe-Zertifikat C1)",
            "Gemischte Modellprüfung über alle Fertigkeiten. Empfohlene Zeit: 90 Minuten.",
            "exam", null, true, 90,
            l1g_mc, l2g_mc, l3g_mc, l5g_mc);
    }
}
