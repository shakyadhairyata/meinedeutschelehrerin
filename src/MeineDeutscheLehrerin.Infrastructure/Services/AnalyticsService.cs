using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface IAnalyticsService
{
    Task<DashboardDto> GetDashboardAsync(string userId, CancellationToken ct = default);
}

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;
    private readonly ICurriculumService _curriculum;
    private const int WeaknessMinAttempts = 3;
    private const int ActivityDays = 14;

    public AnalyticsService(AppDbContext db, ICurriculumService curriculum)
    {
        _db = db;
        _curriculum = curriculum;
    }

    public async Task<DashboardDto> GetDashboardAsync(string userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

        var attempts = await _db.ExerciseAttempts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new { a.Skill, a.GrammarTopic, a.IsCorrect, a.ScorePercent, a.TimeSpentSeconds, a.AttemptedAt, a.ExerciseId })
            .ToListAsync(ct);

        int total = attempts.Count;
        double overallAccuracy = total == 0 ? 0 : Math.Round(100.0 * attempts.Count(a => a.IsCorrect) / total, 1);

        var skillStats = attempts.GroupBy(a => a.Skill)
            .Select(g => new SkillStatDto(
                g.Key, g.Count(),
                Math.Round(100.0 * g.Count(a => a.IsCorrect) / g.Count(), 1),
                Math.Round(g.Average(a => a.ScorePercent), 1)))
            .OrderBy(s => s.Skill)
            .ToList();

        // Weakness detection: grammar topics with enough attempts and the lowest accuracy.
        var topicLevel = await _db.Exercises.AsNoTracking()
            .Join(_db.Lessons, e => e.LessonId, l => l.Id, (e, l) => new { e.Id, l.UnitId })
            .Join(_db.Units, x => x.UnitId, u => u.Id, (x, u) => new { x.Id, u.LevelId })
            .ToDictionaryAsync(x => x.Id, x => x.LevelId, ct);

        var weaknesses = attempts
            .Where(a => !string.IsNullOrEmpty(a.GrammarTopic))
            .GroupBy(a => a.GrammarTopic!)
            .Where(g => g.Count() >= WeaknessMinAttempts)
            .Select(g => new GrammarWeaknessDto(
                g.Key, g.Count(),
                Math.Round(100.0 * g.Count(a => a.IsCorrect) / g.Count(), 1),
                g.Select(a => topicLevel.TryGetValue(a.ExerciseId, out var lv) ? lv : 0).FirstOrDefault()))
            .OrderBy(w => w.Accuracy).ThenByDescending(w => w.Attempts)
            .Take(8)
            .ToList();

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(ActivityDays - 1));
        var activityByDay = attempts
            .Where(a => DateOnly.FromDateTime(a.AttemptedAt.UtcDateTime) >= fromDate)
            .GroupBy(a => DateOnly.FromDateTime(a.AttemptedAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => new
            {
                Attempts = g.Count(),
                Correct = g.Count(a => a.IsCorrect),
                Minutes = (int)Math.Round(g.Sum(a => a.TimeSpentSeconds) / 60.0)
            });

        var activity = Enumerable.Range(0, ActivityDays)
            .Select(i => fromDate.AddDays(i))
            .Select(d => activityByDay.TryGetValue(d, out var v)
                ? new ActivityPointDto(d, v.Attempts, v.Correct, v.Minutes)
                : new ActivityPointDto(d, 0, 0, 0))
            .ToList();

        int lessonsCompleted = await _db.UserLessonProgress
            .CountAsync(p => p.UserId == userId && p.Status == ProgressStatus.Completed, ct);

        int vocabMastered = await _db.UserVocabularyProgress
            .CountAsync(p => p.UserId == userId && p.Box >= 5, ct);

        int minutesStudied = (int)Math.Round(attempts.Sum(a => a.TimeSpentSeconds) / 60.0);

        var levels = await _curriculum.GetLevelsAsync(userId, ct);

        return new DashboardDto(
            user?.DisplayName ?? "Lernende:r",
            user?.TargetLevel,
            user?.CurrentStreak ?? 0,
            user?.LongestStreak ?? 0,
            total,
            overallAccuracy,
            lessonsCompleted,
            vocabMastered,
            minutesStudied,
            skillStats,
            weaknesses,
            activity,
            levels);
    }
}
