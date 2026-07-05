using Microsoft.AspNetCore.Identity;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;

namespace MeineDeutscheLehrerin.Infrastructure.Identity;

/// <summary>
/// Identity user extended with learner profile + streak tracking. Email is the login
/// (set <see cref="IdentityUser.UserName"/> = email on registration).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public CefrLevel? TargetLevel { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Berlin";

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LastActivityDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Monetization: tier gates AI-cost features; the daily counter caps spend (reset per day).
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;
    public DateOnly? AiUsageDate { get; set; }
    public int AiUsageCount { get; set; }
}
