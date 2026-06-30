using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface ICurriculumService
{
    Task<IReadOnlyList<LevelSummaryDto>> GetLevelsAsync(string userId, CancellationToken ct = default);
    Task<LevelDetailDto?> GetLevelAsync(int levelId, string userId, CancellationToken ct = default);
    Task<UnitDetailDto?> GetUnitAsync(int unitId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<PracticeSetDto>> GetPracticeSetsAsync(int levelId, CancellationToken ct = default);
    Task<PracticeSetDetailDto?> GetPracticeSetAsync(int setId, CancellationToken ct = default);
}

public class CurriculumService : ICurriculumService
{
    private readonly AppDbContext _db;
    public CurriculumService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LevelSummaryDto>> GetLevelsAsync(string userId, CancellationToken ct = default)
    {
        var levels = await _db.Levels.AsNoTracking().OrderBy(l => l.Order)
            .Select(l => new
            {
                l,
                UnitCount = l.Units.Count,
                LessonCount = l.Units.SelectMany(u => u.Lessons).Count(),
                VocabularyCount = l.Vocabulary.Count,
                PracticeSetCount = l.PracticeSets.Count
            }).ToListAsync(ct);

        var completedByLevel = await CompletedLessonsByLevelAsync(userId, ct);

        return levels.Select(x =>
        {
            completedByLevel.TryGetValue(x.l.Id, out int done);
            double pct = x.LessonCount == 0 ? 0 : Math.Round(100.0 * done / x.LessonCount, 1);
            return new LevelSummaryDto(x.l.Id, x.l.Code, x.l.Title, x.l.Description, x.l.GoetheExam,
                x.l.Order, x.l.EstimatedDays, x.l.RecommendedMinutesPerDay,
                x.UnitCount, x.LessonCount, x.VocabularyCount, x.PracticeSetCount, pct, done);
        }).ToList();
    }

    public async Task<LevelDetailDto?> GetLevelAsync(int levelId, string userId, CancellationToken ct = default)
    {
        var summary = (await GetLevelsAsync(userId, ct)).FirstOrDefault(l => l.Id == levelId);
        if (summary is null) return null;

        var units = await _db.Units.AsNoTracking().Where(u => u.LevelId == levelId).OrderBy(u => u.Order)
            .Select(u => new { u.Id, u.Title, u.Description, u.Order, u.ThemeTag, LessonCount = u.Lessons.Count })
            .ToListAsync(ct);

        var completedByUnit = await CompletedLessonsByUnitAsync(userId, levelId, ct);

        var unitDtos = units.Select(u =>
        {
            completedByUnit.TryGetValue(u.Id, out int done);
            double pct = u.LessonCount == 0 ? 0 : Math.Round(100.0 * done / u.LessonCount, 1);
            return new UnitSummaryDto(u.Id, u.Title, u.Description, u.Order, u.ThemeTag, u.LessonCount, pct);
        }).ToList();

        return new LevelDetailDto(summary, unitDtos);
    }

    public async Task<UnitDetailDto?> GetUnitAsync(int unitId, string userId, CancellationToken ct = default)
    {
        var unit = await _db.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return null;

        var lessons = await _db.Lessons.AsNoTracking().Where(l => l.UnitId == unitId).OrderBy(l => l.Order)
            .Select(l => new { l.Id, l.Title, l.Skill, l.Order, l.EstimatedMinutes, l.GrammarTopic, ExCount = l.Exercises.Count })
            .ToListAsync(ct);

        var progress = await _db.UserLessonProgress.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Where(p => lessons.Select(l => l.Id).Contains(p.LessonId))
            .ToDictionaryAsync(p => p.LessonId, ct);

        var lessonDtos = lessons.Select(l =>
        {
            progress.TryGetValue(l.Id, out var pr);
            return new LessonSummaryDto(l.Id, l.Title, l.Skill, l.Order, l.EstimatedMinutes, l.GrammarTopic,
                l.ExCount, pr?.Status ?? ProgressStatus.NotStarted, pr?.ScorePercent ?? 0);
        }).ToList();

        return new UnitDetailDto(unit.Id, unit.Title, unit.Description, unit.Order, unit.ThemeTag, lessonDtos);
    }

    public async Task<IReadOnlyList<PracticeSetDto>> GetPracticeSetsAsync(int levelId, CancellationToken ct = default)
    {
        return await _db.PracticeSets.AsNoTracking().Where(s => s.LevelId == levelId).OrderBy(s => s.Order)
            .Select(s => new PracticeSetDto(s.Id, s.Title, s.Description, s.Skill, s.Kind, s.IsExam,
                s.TimeLimitMinutes, s.Items.Count))
            .ToListAsync(ct);
    }

    public async Task<PracticeSetDetailDto?> GetPracticeSetAsync(int setId, CancellationToken ct = default)
    {
        var set = await _db.PracticeSets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == setId, ct);
        if (set is null) return null;

        var exercises = await _db.PracticeSetItems.AsNoTracking()
            .Where(i => i.PracticeSetId == setId).OrderBy(i => i.Order)
            .Select(i => i.Exercise!).ToListAsync(ct);

        var dto = set.ToDto(exercises.Count);
        return new PracticeSetDetailDto(dto, exercises.Select(e => e.ToDto()).ToList());
    }

    private async Task<Dictionary<int, int>> CompletedLessonsByLevelAsync(string userId, CancellationToken ct) =>
        await (from p in _db.UserLessonProgress.AsNoTracking()
               where p.UserId == userId && p.Status == ProgressStatus.Completed
               join l in _db.Lessons on p.LessonId equals l.Id
               join u in _db.Units on l.UnitId equals u.Id
               group p by u.LevelId into g
               select new { LevelId = g.Key, Count = g.Count() })
              .ToDictionaryAsync(x => x.LevelId, x => x.Count, ct);

    private async Task<Dictionary<int, int>> CompletedLessonsByUnitAsync(string userId, int levelId, CancellationToken ct) =>
        await (from p in _db.UserLessonProgress.AsNoTracking()
               where p.UserId == userId && p.Status == ProgressStatus.Completed
               join l in _db.Lessons on p.LessonId equals l.Id
               join u in _db.Units on l.UnitId equals u.Id
               where u.LevelId == levelId
               group p by u.Id into g
               select new { UnitId = g.Key, Count = g.Count() })
              .ToDictionaryAsync(x => x.UnitId, x => x.Count, ct);
}
