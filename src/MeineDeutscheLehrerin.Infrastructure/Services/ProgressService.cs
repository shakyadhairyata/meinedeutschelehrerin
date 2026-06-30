using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface IProgressService
{
    Task<LessonDetailDto?> GetLessonAsync(int lessonId, string userId, CancellationToken ct = default);
    Task<GradeResultDto?> SubmitAttemptAsync(string userId, SubmitAttemptRequest req, CancellationToken ct = default);
    Task<bool> CompleteLessonAsync(string userId, CompleteLessonRequest req, CancellationToken ct = default);
}

public class ProgressService : IProgressService
{
    private readonly AppDbContext _db;
    private readonly IExerciseGrader _grader;
    private readonly ILanguageService _language;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ProgressService(AppDbContext db, IExerciseGrader grader, ILanguageService language)
    {
        _db = db;
        _grader = grader;
        _language = language;
    }

    public async Task<LessonDetailDto?> GetLessonAsync(int lessonId, string userId, CancellationToken ct = default)
    {
        var lesson = await _db.Lessons.AsNoTracking()
            .Include(l => l.Exercises.OrderBy(e => e.Order))
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);
        if (lesson is null) return null;

        var progress = await EnsureLessonStartedAsync(userId, lessonId, ct);

        return new LessonDetailDto(
            lesson.Id, lesson.UnitId, lesson.Title, lesson.Skill, lesson.Content,
            lesson.AudioScript, lesson.GrammarTopic, lesson.EstimatedMinutes,
            progress.Status, progress.ScorePercent,
            lesson.Exercises.Select(e => e.ToDto()).ToList());
    }

    public async Task<GradeResultDto?> SubmitAttemptAsync(string userId, SubmitAttemptRequest req, CancellationToken ct = default)
    {
        var exercise = await _db.Exercises.AsNoTracking().FirstOrDefaultAsync(e => e.Id == req.ExerciseId, ct);
        if (exercise is null) return null;

        GradeResultDto result;
        JsonNode? feedbackNode = null;

        if (_grader.CanAutoGrade(exercise.Type))
        {
            var outcome = _grader.Grade(exercise, req.Response);
            result = new GradeResultDto(outcome.IsCorrect, outcome.ScorePercent, exercise.Explanation,
                outcome.CorrectAnswer, null);
        }
        else
        {
            var level = await LevelOfExerciseAsync(exercise.Id, ct);
            var content = string.IsNullOrWhiteSpace(exercise.ContentJson) ? null : JsonNode.Parse(exercise.ContentJson);

            if (exercise.Type == ExerciseType.Writing)
            {
                var text = req.Response?["text"]?.GetValue<string>() ?? "";
                var minWords = content?["minWords"]?.GetValue<int?>() ?? 40;
                var fb = await _language.EvaluateWritingAsync(exercise.Prompt, text, level, minWords, ct);
                feedbackNode = JsonSerializer.SerializeToNode(fb, JsonOpts);
                result = new GradeResultDto(fb.ScorePercent >= 60, fb.ScorePercent, exercise.Explanation, null, feedbackNode);
            }
            else // Speaking
            {
                var transcript = req.Response?["transcript"]?.GetValue<string>() ?? "";
                var target = content?["targetText"]?.GetValue<string>() ?? exercise.Prompt;
                var fb = await _language.EvaluateSpeakingAsync(target, transcript, level, ct);
                feedbackNode = JsonSerializer.SerializeToNode(fb, JsonOpts);
                result = new GradeResultDto(fb.ScorePercent >= 60, fb.ScorePercent, exercise.Explanation, null, feedbackNode);
            }
        }

        _db.ExerciseAttempts.Add(new ExerciseAttempt
        {
            UserId = userId,
            ExerciseId = exercise.Id,
            Skill = exercise.Skill,
            GrammarTopic = exercise.GrammarTopic,
            IsCorrect = result.IsCorrect,
            ScorePercent = result.ScorePercent,
            ResponseJson = req.Response?.ToJsonString() ?? "{}",
            FeedbackJson = feedbackNode?.ToJsonString(),
            TimeSpentSeconds = req.TimeSpentSeconds,
            AttemptedAt = DateTimeOffset.UtcNow
        });

        await UpdateStreakAsync(userId, ct);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<bool> CompleteLessonAsync(string userId, CompleteLessonRequest req, CancellationToken ct = default)
    {
        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == req.LessonId, ct);
        if (!lessonExists) return false;

        var exerciseIds = await _db.Exercises.Where(e => e.LessonId == req.LessonId).Select(e => e.Id).ToListAsync(ct);

        // Best score per exercise from the user's attempts → average = lesson score.
        double score = 100;
        if (exerciseIds.Count > 0)
        {
            var best = await _db.ExerciseAttempts
                .Where(a => a.UserId == userId && exerciseIds.Contains(a.ExerciseId))
                .GroupBy(a => a.ExerciseId)
                .Select(g => g.Max(a => a.ScorePercent))
                .ToListAsync(ct);
            score = best.Count == 0 ? 0 : Math.Round(best.Sum() / exerciseIds.Count, 1);
        }

        var progress = await _db.UserLessonProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == req.LessonId, ct);
        if (progress is null)
        {
            progress = new UserLessonProgress { UserId = userId, LessonId = req.LessonId, StartedAt = DateTimeOffset.UtcNow };
            _db.UserLessonProgress.Add(progress);
        }

        progress.Status = ProgressStatus.Completed;
        progress.ScorePercent = score;
        progress.TimeSpentSeconds += req.TimeSpentSeconds;
        progress.CompletedAt = DateTimeOffset.UtcNow;
        progress.UpdatedAt = DateTimeOffset.UtcNow;

        await UpdateStreakAsync(userId, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<UserLessonProgress> EnsureLessonStartedAsync(string userId, int lessonId, CancellationToken ct)
    {
        var progress = await _db.UserLessonProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId, ct);
        if (progress is null)
        {
            progress = new UserLessonProgress
            {
                UserId = userId,
                LessonId = lessonId,
                Status = ProgressStatus.InProgress,
                StartedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.UserLessonProgress.Add(progress);
            await _db.SaveChangesAsync(ct);
        }
        return progress;
    }

    private async Task<CefrLevel> LevelOfExerciseAsync(int exerciseId, CancellationToken ct)
    {
        var code = await (from e in _db.Exercises
                          join l in _db.Lessons on e.LessonId equals l.Id
                          join u in _db.Units on l.UnitId equals u.Id
                          join lv in _db.Levels on u.LevelId equals lv.Id
                          where e.Id == exerciseId
                          select (CefrLevel?)lv.Code).FirstOrDefaultAsync(ct);
        return code ?? CefrLevel.A1;
    }

    private async Task UpdateStreakAsync(string userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (user.LastActivityDate == today) return;

        if (user.LastActivityDate == today.AddDays(-1)) user.CurrentStreak += 1;
        else user.CurrentStreak = 1;

        user.LongestStreak = Math.Max(user.LongestStreak, user.CurrentStreak);
        user.LastActivityDate = today;
    }
}
