namespace MeineDeutscheLehrerin.Domain.Entities;

/// <summary>Per-user completion state for a lesson. UserId is the Identity user id (string GUID).</summary>
public class UserLessonProgress
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
    public double ScorePercent { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One graded attempt at one exercise. The append-only stream behind all analytics —
/// weakness detection groups these by <see cref="Skill"/> and <see cref="GrammarTopic"/>.
/// </summary>
public class ExerciseAttempt
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    public SkillType Skill { get; set; }
    public string? GrammarTopic { get; set; }

    public bool IsCorrect { get; set; }
    /// <summary>0..100. Binary for auto-graded items; granular for AI-scored writing/speaking.</summary>
    public double ScorePercent { get; set; }

    /// <summary>The learner's raw response (JSON), kept for review and re-grading.</summary>
    public string ResponseJson { get; set; } = "{}";
    /// <summary>AI feedback (JSON) for Speaking/Writing; null for auto-graded items.</summary>
    public string? FeedbackJson { get; set; }

    public int TimeSpentSeconds { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Spaced-repetition state for one vocabulary item for one user (Leitner-style boxes 0..5).
/// </summary>
public class UserVocabularyProgress
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int VocabularyItemId { get; set; }
    public VocabularyItem? VocabularyItem { get; set; }

    /// <summary>Leitner box 0..5; higher = longer interval before next review.</summary>
    public int Box { get; set; }
    public int TimesSeen { get; set; }
    public int TimesCorrect { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public DateTimeOffset NextReviewAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A personalised 2-week study plan instance for one user working through one level.</summary>
public class UserStudyPlan
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int LevelId { get; set; }
    public Level? Level { get; set; }

    public DateOnly StartDate { get; set; }
    public int MinutesPerDay { get; set; } = 90;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    public List<StudyPlanDay> Days { get; set; } = new();
}

public class StudyPlanDay
{
    public int Id { get; set; }
    public int StudyPlanId { get; set; }
    public UserStudyPlan? StudyPlan { get; set; }

    public int DayNumber { get; set; }
    public DateOnly Date { get; set; }
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }
    public int TargetMinutes { get; set; }
    public bool Completed { get; set; }
}
