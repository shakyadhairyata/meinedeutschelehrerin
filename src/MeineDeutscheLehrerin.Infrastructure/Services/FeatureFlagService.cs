using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface IFeatureFlagService
{
    Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default);
    Task<Dictionary<string, bool>> GetMapAsync(CancellationToken ct = default);
    Task<bool> IsEnabledAsync(string key, CancellationToken ct = default);
    Task<FeatureFlag?> SetAsync(string key, bool enabled, CancellationToken ct = default);
}

/// <summary>
/// Reads and toggles feature flags. Flags are seeded on startup; an unknown key is treated as
/// enabled so a missing row never silently hides a feature.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly AppDbContext _db;
    public FeatureFlagService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default) =>
        await _db.FeatureFlags.AsNoTracking().OrderBy(f => f.Key).ToListAsync(ct);

    public async Task<Dictionary<string, bool>> GetMapAsync(CancellationToken ct = default) =>
        await _db.FeatureFlags.AsNoTracking().ToDictionaryAsync(f => f.Key, f => f.Enabled, ct);

    public async Task<bool> IsEnabledAsync(string key, CancellationToken ct = default)
    {
        var flag = await _db.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Key == key, ct);
        return flag?.Enabled ?? true;
    }

    public async Task<FeatureFlag?> SetAsync(string key, bool enabled, CancellationToken ct = default)
    {
        var flag = await _db.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, ct);
        if (flag is null) return null;
        flag.Enabled = enabled;
        flag.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return flag;
    }

    /// <summary>Idempotently inserts any missing default flags. Existing flags are left untouched.</summary>
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.FeatureFlags.Select(f => f.Key).ToListAsync(ct);
        var missing = FeatureKeys.Defaults.Where(d => !existing.Contains(d.Key))
            .Select(d => new FeatureFlag { Key = d.Key, Enabled = true, Description = d.Description });
        db.FeatureFlags.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }
}
