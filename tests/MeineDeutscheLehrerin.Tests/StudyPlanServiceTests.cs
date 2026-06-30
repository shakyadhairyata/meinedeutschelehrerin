using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Data;
using MeineDeutscheLehrerin.Infrastructure.Seeding;
using MeineDeutscheLehrerin.Infrastructure.Services;
using Xunit;

namespace MeineDeutscheLehrerin.Tests;

/// <summary>
/// Regression tests for the study plan. Notably covers GetActivePlanAsync, which previously
/// threw on SQLite because it ordered by a DateTimeOffset column.
/// </summary>
public class StudyPlanServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;
    private readonly StudyPlanService _service;

    public StudyPlanServiceTests()
    {
        _conn = new SqliteConnection("Filename=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        DbSeeder.SeedAsync(_db).GetAwaiter().GetResult();
        _service = new StudyPlanService(_db);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    [Fact]
    public async Task GetActivePlan_returns_null_when_none_exists()
    {
        // Must not throw on SQLite (regression: ORDER BY DateTimeOffset).
        var plan = await _service.GetActivePlanAsync("user-1");
        Assert.Null(plan);
    }

    [Fact]
    public async Task CreatePlan_then_GetActivePlan_returns_it_with_one_day_per_unit()
    {
        var a1 = await _db.Levels.FirstAsync();
        var unitCount = await _db.Units.CountAsync(u => u.LevelId == a1.Id);
        var expectedDays = Math.Max(a1.EstimatedDays, unitCount);

        var created = await _service.CreatePlanAsync("user-1",
            new CreateStudyPlanRequest(a1.Id, new DateOnly(2026, 6, 29), 90));
        Assert.NotNull(created);
        // Full 2-week schedule, and every unit is represented across the days.
        Assert.Equal(expectedDays, created!.Days.Count);
        Assert.Equal(unitCount, created.Days.Select(d => d.UnitId).Distinct().Count());

        var active = await _service.GetActivePlanAsync("user-1");
        Assert.NotNull(active);
        Assert.Equal(created.Id, active!.Id);
    }

    [Fact]
    public async Task CreatePlan_deactivates_the_previous_active_plan()
    {
        var a1 = await _db.Levels.FirstAsync();
        await _service.CreatePlanAsync("user-1", new CreateStudyPlanRequest(a1.Id, new DateOnly(2026, 6, 1), 60));
        var second = await _service.CreatePlanAsync("user-1", new CreateStudyPlanRequest(a1.Id, new DateOnly(2026, 6, 29), 90));

        var active = await _service.GetActivePlanAsync("user-1");
        Assert.Equal(second!.Id, active!.Id);
        Assert.Equal(1, await _db.UserStudyPlans.CountAsync(p => p.UserId == "user-1" && p.IsActive));
    }
}
