namespace MeineDeutscheLehrerin.Domain;

/// <summary>CEFR proficiency levels, ordered A1 → C1. Maps to Goethe-Institut exams.</summary>
public enum CefrLevel
{
    A1 = 1,
    A2 = 2,
    B1 = 3,
    B2 = 4,
    C1 = 5
}

/// <summary>The six competence areas a learner trains.</summary>
public enum SkillType
{
    Grammar = 1,
    Vocabulary = 2,
    Reading = 3,
    Listening = 4,
    Speaking = 5,
    Writing = 6
}

/// <summary>
/// The interactive exercise formats the engine can render and grade.
/// Auto-gradable types are graded server-side; Speaking/Writing are scored by the
/// Python language-service (Claude) with a deterministic offline fallback.
/// </summary>
public enum ExerciseType
{
    MultipleChoice = 1,
    FillInBlank = 2,
    Cloze = 3,
    Matching = 4,
    Reorder = 5,            // build a sentence from shuffled tokens
    ReadingComprehension = 6,
    ListeningComprehension = 7,
    Dictation = 8,
    Conjugation = 9,
    Translation = 10,
    Speaking = 11,          // AI-scored
    Writing = 12            // AI-scored
}

public enum ProgressStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

public static class ExerciseTypeExtensions
{
    /// <summary>True when the engine can grade the response without the language-service.</summary>
    public static bool IsAutoGradable(this ExerciseType type) =>
        type is not (ExerciseType.Speaking or ExerciseType.Writing);
}
