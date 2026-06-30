namespace MeineDeutscheLehrerin.Domain.Entities;

/// <summary>
/// A CEFR level (A1..C1). Designed to be completable in ~2 weeks: each level owns
/// ~14 <see cref="Unit"/>s (one per study day).
/// </summary>
public class Level
{
    public int Id { get; set; }
    public CefrLevel Code { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string GoetheExam { get; set; } = "";
    public int Order { get; set; }
    public int EstimatedDays { get; set; } = 14;
    public int RecommendedMinutesPerDay { get; set; } = 90;

    public List<Unit> Units { get; set; } = new();
    public List<VocabularyItem> Vocabulary { get; set; } = new();
    public List<PracticeSet> PracticeSets { get; set; } = new();
}

/// <summary>A themed study day inside a level (e.g. "Sich vorstellen — Introducing yourself").</summary>
public class Unit
{
    public int Id { get; set; }
    public int LevelId { get; set; }
    public Level? Level { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Day number within the 2-week plan (1..EstimatedDays).</summary>
    public int Order { get; set; }
    public string ThemeTag { get; set; } = "";

    public List<Lesson> Lessons { get; set; } = new();
}

/// <summary>
/// A single teachable chunk within a unit, scoped to one <see cref="SkillType"/>.
/// <see cref="Content"/> holds the teaching material (Markdown). Reading/Listening
/// lessons put the passage / audio script here and attach comprehension exercises.
/// </summary>
public class Lesson
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }

    public string Title { get; set; } = "";
    public SkillType Skill { get; set; }
    public int Order { get; set; }

    /// <summary>Teaching material in Markdown (grammar explanation, reading passage, etc.).</summary>
    public string Content { get; set; } = "";

    /// <summary>For Listening/Dictation lessons: German script the client speaks via TTS.</summary>
    public string? AudioScript { get; set; }

    /// <summary>Canonical grammar topic tag (e.g. "Präsens", "Akkusativ") for weakness analysis.</summary>
    public string? GrammarTopic { get; set; }

    public int EstimatedMinutes { get; set; } = 15;

    public List<Exercise> Exercises { get; set; } = new();
}

/// <summary>
/// An interactive, gradable item. <see cref="ContentJson"/> is the type-specific payload
/// shown to the learner; <see cref="SolutionJson"/> holds the answer key (never sent to
/// the client for auto-graded types). Schemas are documented in ExerciseGrader.
/// </summary>
public class Exercise
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public ExerciseType Type { get; set; }
    public SkillType Skill { get; set; }

    /// <summary>Stem / instruction shown above the interactive widget.</summary>
    public string Prompt { get; set; } = "";

    public string ContentJson { get; set; } = "{}";
    public string SolutionJson { get; set; } = "{}";

    /// <summary>Shown after grading to explain the correct answer.</summary>
    public string Explanation { get; set; } = "";

    public string? GrammarTopic { get; set; }
    public int Points { get; set; } = 10;
    public int Order { get; set; }
    /// <summary>1 (easy) .. 5 (hard); used to weight analytics and SRS.</summary>
    public int Difficulty { get; set; } = 1;

    public List<PracticeSetItem> PracticeSetItems { get; set; } = new();
}

/// <summary>
/// A curated collection of exercises: a full practice set, a skill drill, or a mock Goethe
/// exam section. Lets the same exercise appear in lesson practice and in exam sets.
/// </summary>
public class PracticeSet
{
    public int Id { get; set; }
    public int LevelId { get; set; }
    public Level? Level { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Optional skill focus; null = mixed (e.g. a full mock exam).</summary>
    public SkillType? Skill { get; set; }
    /// <summary>"drill", "exam", "review" — drives UI presentation and scoring.</summary>
    public string Kind { get; set; } = "drill";
    public bool IsExam { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int Order { get; set; }

    public List<PracticeSetItem> Items { get; set; } = new();
}

public class PracticeSetItem
{
    public int Id { get; set; }
    public int PracticeSetId { get; set; }
    public PracticeSet? PracticeSet { get; set; }
    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }
    public int Order { get; set; }
}

/// <summary>A vocabulary lemma, trained via spaced repetition (<see cref="UserVocabularyProgress"/>).</summary>
public class VocabularyItem
{
    public int Id { get; set; }
    public int LevelId { get; set; }
    public Level? Level { get; set; }

    public string German { get; set; } = "";
    public string English { get; set; } = "";
    public string PartOfSpeech { get; set; } = "";
    /// <summary>der/die/das for nouns; null otherwise.</summary>
    public string? Article { get; set; }
    public string? Plural { get; set; }
    public string ExampleSentence { get; set; } = "";
    public string? Note { get; set; }
    public string ThemeTag { get; set; } = "";
}
