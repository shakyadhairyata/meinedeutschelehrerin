using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public interface IVocabularyService
{
    Task<IReadOnlyList<VocabularyDto>> GetDeckAsync(int levelId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<VocabularyDto>> GetDueAsync(int levelId, string userId, int limit, CancellationToken ct = default);
    Task<VocabularyDto?> ReviewAsync(string userId, VocabReviewRequest req, CancellationToken ct = default);
}

/// <summary>Leitner spaced-repetition over a level's vocabulary.</summary>
public class VocabularyService : IVocabularyService
{
    private readonly AppDbContext _db;
    public VocabularyService(AppDbContext db) => _db = db;

    // Days until next review per Leitner box (index = box 0..5).
    private static readonly int[] IntervalDays = { 0, 1, 2, 4, 7, 14 };

    public async Task<IReadOnlyList<VocabularyDto>> GetDeckAsync(int levelId, string userId, CancellationToken ct = default)
    {
        var items = await _db.VocabularyItems.AsNoTracking()
            .Where(v => v.LevelId == levelId).OrderBy(v => v.ThemeTag).ThenBy(v => v.German)
            .ToListAsync(ct);
        var progress = await _db.UserVocabularyProgress.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.VocabularyItemId, ct);

        return items.Select(v => v.ToDto(progress.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<IReadOnlyList<VocabularyDto>> GetDueAsync(int levelId, string userId, int limit, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var items = await _db.VocabularyItems.AsNoTracking()
            .Where(v => v.LevelId == levelId).ToListAsync(ct);
        var progress = await _db.UserVocabularyProgress.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.VocabularyItemId, ct);

        // Due = never seen, or NextReviewAt has passed. New items first, then most overdue.
        var due = items
            .Select(v => new { v, p = progress.GetValueOrDefault(v.Id) })
            .Where(x => x.p is null || x.p.NextReviewAt <= now)
            .OrderBy(x => x.p is null ? 0 : 1)
            .ThenBy(x => x.p?.NextReviewAt ?? DateTimeOffset.MinValue)
            .Take(limit)
            .Select(x => x.v.ToDto(x.p))
            .ToList();
        return due;
    }

    public async Task<VocabularyDto?> ReviewAsync(string userId, VocabReviewRequest req, CancellationToken ct = default)
    {
        var item = await _db.VocabularyItems.AsNoTracking().FirstOrDefaultAsync(v => v.Id == req.VocabularyItemId, ct);
        if (item is null) return null;

        var p = await _db.UserVocabularyProgress
            .FirstOrDefaultAsync(x => x.UserId == userId && x.VocabularyItemId == req.VocabularyItemId, ct);
        if (p is null)
        {
            p = new UserVocabularyProgress { UserId = userId, VocabularyItemId = req.VocabularyItemId };
            _db.UserVocabularyProgress.Add(p);
        }

        p.TimesSeen += 1;
        if (req.Correct)
        {
            p.TimesCorrect += 1;
            p.Box = Math.Min(p.Box + 1, IntervalDays.Length - 1);
        }
        else
        {
            p.Box = 0;
        }
        p.LastReviewedAt = DateTimeOffset.UtcNow;
        p.NextReviewAt = DateTimeOffset.UtcNow.AddDays(IntervalDays[p.Box]);

        await UpdateStreakAsync(userId, ct);
        await _db.SaveChangesAsync(ct);
        return item.ToDto(p);
    }

    private async Task UpdateStreakAsync(string userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (user.LastActivityDate == today) return;
        user.CurrentStreak = user.LastActivityDate == today.AddDays(-1) ? user.CurrentStreak + 1 : 1;
        user.LongestStreak = Math.Max(user.LongestStreak, user.CurrentStreak);
        user.LastActivityDate = today;
    }
}
