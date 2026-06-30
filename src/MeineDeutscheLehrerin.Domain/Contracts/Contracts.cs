using System.Text.Json.Nodes;

namespace MeineDeutscheLehrerin.Domain.Contracts;

// ---------- Curriculum ----------

public record LevelSummaryDto(
    int Id, CefrLevel Code, string Title, string Description, string GoetheExam,
    int Order, int EstimatedDays, int RecommendedMinutesPerDay,
    int UnitCount, int LessonCount, int VocabularyCount, int PracticeSetCount,
    double ProgressPercent, int CompletedLessons);

public record UnitSummaryDto(
    int Id, string Title, string Description, int Order, string ThemeTag,
    int LessonCount, double ProgressPercent);

public record LevelDetailDto(LevelSummaryDto Level, IReadOnlyList<UnitSummaryDto> Units);

public record LessonSummaryDto(
    int Id, string Title, SkillType Skill, int Order, int EstimatedMinutes,
    string? GrammarTopic, int ExerciseCount, ProgressStatus Status, double ScorePercent);

public record UnitDetailDto(
    int Id, string Title, string Description, int Order, string ThemeTag,
    IReadOnlyList<LessonSummaryDto> Lessons);

public record ExerciseDto(
    int Id, ExerciseType Type, SkillType Skill, string Prompt,
    JsonNode? Content, string? GrammarTopic, int Points, int Difficulty);

public record LessonDetailDto(
    int Id, int UnitId, string Title, SkillType Skill, string Content,
    string? AudioScript, string? GrammarTopic, int EstimatedMinutes,
    ProgressStatus Status, double ScorePercent, IReadOnlyList<ExerciseDto> Exercises);

public record VocabularyDto(
    int Id, string German, string English, string PartOfSpeech, string? Article,
    string? Plural, string ExampleSentence, string? Note, string ThemeTag,
    int Box, DateTimeOffset? NextReviewAt);

public record PracticeSetDto(
    int Id, string Title, string Description, SkillType? Skill, string Kind,
    bool IsExam, int? TimeLimitMinutes, int ExerciseCount);

public record PracticeSetDetailDto(PracticeSetDto Set, IReadOnlyList<ExerciseDto> Exercises);

// ---------- Grading & attempts ----------

public record SubmitAttemptRequest(int ExerciseId, JsonNode? Response, int TimeSpentSeconds);

public record GradeResultDto(
    bool IsCorrect, double ScorePercent, string Explanation,
    JsonNode? CorrectAnswer, JsonNode? Feedback);

public record CompleteLessonRequest(int LessonId, int TimeSpentSeconds);

// ---------- Vocabulary SRS ----------

public record VocabReviewRequest(int VocabularyItemId, bool Correct);

// ---------- Analytics ----------

public record SkillStatDto(SkillType Skill, int Attempts, double Accuracy, double AvgScore);

public record GrammarWeaknessDto(string GrammarTopic, int Attempts, double Accuracy, int LevelId);

public record ActivityPointDto(DateOnly Date, int Attempts, int CorrectCount, int MinutesStudied);

public record DashboardDto(
    string DisplayName,
    CefrLevel? TargetLevel,
    int CurrentStreak,
    int LongestStreak,
    int TotalAttempts,
    double OverallAccuracy,
    int LessonsCompleted,
    int VocabularyMastered,
    int MinutesStudied,
    IReadOnlyList<SkillStatDto> SkillStats,
    IReadOnlyList<GrammarWeaknessDto> TopWeaknesses,
    IReadOnlyList<ActivityPointDto> RecentActivity,
    IReadOnlyList<LevelSummaryDto> Levels);

// ---------- Study plan ----------

public record CreateStudyPlanRequest(int LevelId, DateOnly StartDate, int MinutesPerDay);

public record StudyPlanDayDto(
    int DayNumber, DateOnly Date, int UnitId, string UnitTitle, string ThemeTag,
    int TargetMinutes, bool Completed, double UnitProgressPercent);

public record StudyPlanDto(
    int Id, int LevelId, CefrLevel LevelCode, DateOnly StartDate, int MinutesPerDay,
    bool IsActive, IReadOnlyList<StudyPlanDayDto> Days);

// ---------- Language-service (Python/Claude) ----------

public record WritingFeedbackDto(
    double ScorePercent, string Summary,
    IReadOnlyList<string> Strengths, IReadOnlyList<WritingCorrectionDto> Corrections,
    string CorrectedText, string CefrEstimate);

public record WritingCorrectionDto(string Original, string Correction, string Explanation, string Category);

public record SpeakingFeedbackDto(
    double ScorePercent, string Transcript, string Summary,
    IReadOnlyList<string> PronunciationTips, double AccuracyVsTarget);

public record GeneratedExercisesDto(IReadOnlyList<GeneratedExerciseDto> Exercises);

public record GeneratedVocabularyDto(IReadOnlyList<GeneratedVocabItemDto> Items);

public record GeneratedVocabItemDto(
    string German, string English, string PartOfSpeech,
    string? Article, string? Plural, string Example, string Theme);

public record GeneratedExerciseDto(
    ExerciseType Type, SkillType Skill, string Prompt, JsonNode? Content,
    JsonNode? Solution, string Explanation, string? GrammarTopic, int Difficulty);
