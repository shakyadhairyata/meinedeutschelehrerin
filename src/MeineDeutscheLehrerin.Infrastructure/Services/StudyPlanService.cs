using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface IStudyPlanService
{
    Task<StudyPlanDto?> GetActivePlanAsync(string userId, CancellationToken ct = default);
    Task<StudyPlanDto?> CreatePlanAsync(string userId, CreateStudyPlanRequest req, CancellationToken ct = default);
}

/// <summary>
/// Builds a personalised 2-week plan that maps each unit of a level onto a study day,
/// so a learner can finish a level in the level's <see cref="Level.EstimatedDays"/> window.
/// </summary>
public class StudyPlanService : IStudyPlanService
{
    private readonly AppDbContext _db;
    public StudyPlanService(AppDbContext db) => _db = db;

    public async Task<StudyPlanDto?> GetActivePlanAsync(string userId, CancellationToken ct = default)
    {
        // Order by Id (not CreatedAt): newest plan has the highest Id, and SQLite cannot
        // ORDER BY a DateTimeOffset column.
        var plan = await _db.UserStudyPlans.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        return plan is null ? null : await BuildDtoAsync(plan, userId, ct);
    }

    public async Task<StudyPlanDto?> CreatePlanAsync(string userId, CreateStudyPlanRequest req, CancellationToken ct = default)
    {
        var level = await _db.Levels.AsNoTracking().FirstOrDefaultAsync(l => l.Id == req.LevelId, ct);
        if (level is null) return null;

        var units = await _db.Units.AsNoTracking()
            .Where(u => u.LevelId == req.LevelId).OrderBy(u => u.Order).ToListAsync(ct);
        if (units.Count == 0) return null;

        // Deactivate any existing plans for this user.
        await _db.UserStudyPlans.Where(p => p.UserId == userId && p.IsActive)
            .ForEachAsync(p => p.IsActive = false, ct);

        var plan = new UserStudyPlan
        {
            UserId = userId,
            LevelId = req.LevelId,
            StartDate = req.StartDate,
            MinutesPerDay = req.MinutesPerDay <= 0 ? level.RecommendedMinutesPerDay : req.MinutesPerDay,
            IsActive = true
        };

        // Build a full 2-week schedule: spread the level's units evenly across EstimatedDays so a
        // shorter level (e.g. 7 units) still fills ~14 days (≈2 days per unit — study then practice).
        // If a level has more units than days, each unit still gets its own day.
        int days = Math.Max(level.EstimatedDays, units.Count);
        for (int d = 0; d < days; d++)
        {
            int unitIndex = (int)((long)d * units.Count / days);
            plan.Days.Add(new StudyPlanDay
            {
                DayNumber = d + 1,
                Date = req.StartDate.AddDays(d),
                UnitId = units[unitIndex].Id,
                TargetMinutes = plan.MinutesPerDay
            });
        }

        _db.UserStudyPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(plan, userId, ct);
    }

    private async Task<StudyPlanDto> BuildDtoAsync(UserStudyPlan plan, string userId, CancellationToken ct)
    {
        var level = await _db.Levels.AsNoTracking().FirstAsync(l => l.Id == plan.LevelId, ct);
        var days = await _db.StudyPlanDays.AsNoTracking()
            .Where(d => d.StudyPlanId == plan.Id).OrderBy(d => d.DayNumber).ThenBy(d => d.Id)
            .Select(d => new { d.DayNumber, d.Date, d.UnitId, d.TargetMinutes, d.Completed,
                UnitTitle = d.Unit!.Title, d.Unit.ThemeTag, LessonCount = d.Unit.Lessons.Count })
            .ToListAsync(ct);

        var unitIds = days.Select(d => d.UnitId).ToList();
        var completedByUnit = await (from p in _db.UserLessonProgress.AsNoTracking()
                                     where p.UserId == userId && p.Status == ProgressStatus.Completed
                                     join l in _db.Lessons on p.LessonId equals l.Id
                                     where unitIds.Contains(l.UnitId)
                                     group p by l.UnitId into g
                                     select new { UnitId = g.Key, Count = g.Count() })
                                    .ToDictionaryAsync(x => x.UnitId, x => x.Count, ct);

        var dayDtos = days.Select(d =>
        {
            completedByUnit.TryGetValue(d.UnitId, out int done);
            double pct = d.LessonCount == 0 ? 0 : Math.Round(100.0 * done / d.LessonCount, 1);
            return new StudyPlanDayDto(d.DayNumber, d.Date, d.UnitId, d.UnitTitle, d.ThemeTag,
                d.TargetMinutes, pct >= 100, pct);
        }).ToList();

        return new StudyPlanDto(plan.Id, plan.LevelId, level.Code, plan.StartDate,
            plan.MinutesPerDay, plan.IsActive, dayDtos);
    }
}
