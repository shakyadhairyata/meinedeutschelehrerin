using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public record AiAccessStatus(string Tier, int UsageToday, int DailyLimit);

public interface IAiAccessService
{
    /// <summary>True if the user may make an AI call now (and consumes one from the daily quota).</summary>
    Task<bool> TryConsumeAsync(string userId, CancellationToken ct = default);
    Task<AiAccessStatus> GetStatusAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Gates AI-backed calls: the ai_feedback flag must be on, the user must be on the Paid tier, and
/// they must be under the per-day quota. This is what protects the Anthropic balance — a Free or
/// over-quota user never reaches Claude; the caller falls back to the deterministic scorer.
/// </summary>
public class AiAccessService : IAiAccessService
{
    private readonly AppDbContext _db;
    private readonly IFeatureFlagService _flags;
    private readonly int _freeLimit;
    private readonly int _paidLimit;

    public AiAccessService(AppDbContext db, IFeatureFlagService flags, IConfiguration config)
    {
        _db = db;
        _flags = flags;
        _freeLimit = config.GetValue("Ai:FreeDailyLimit", 0);
        _paidLimit = config.GetValue("Ai:PaidDailyLimit", 30);
    }

    private int LimitFor(SubscriptionTier tier) => tier == SubscriptionTier.Paid ? _paidLimit : _freeLimit;

    public async Task<bool> TryConsumeAsync(string userId, CancellationToken ct = default)
    {
        if (!await _flags.IsEnabledAsync(FeatureKeys.AiFeedback, ct)) return false;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (user.AiUsageDate != today) { user.AiUsageDate = today; user.AiUsageCount = 0; }

        if (user.AiUsageCount >= LimitFor(user.Tier)) return false;

        user.AiUsageCount++;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AiAccessStatus> GetStatusAsync(string userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        var tier = user?.Tier ?? SubscriptionTier.Free;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var used = (user?.AiUsageDate == today) ? user!.AiUsageCount : 0;
        return new AiAccessStatus(tier.ToString(), used, LimitFor(tier));
    }
}
