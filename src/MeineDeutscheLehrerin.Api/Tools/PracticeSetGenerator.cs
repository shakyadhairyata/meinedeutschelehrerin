using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Api.Tools;

/// <summary>
/// Assembles full Goethe-style practice sets from a level's existing exercises — a balanced mix
/// across grammar, reading, listening, writing and speaking. Idempotent: sets are marked in the
/// title, so re-running only tops a level up to the requested count.
/// </summary>
public static class PracticeSetGenerator
{
    private const string Marker = "Modellsatz";

    // Rough Goethe-style shape per set; skills with no exercises at a level are skipped.
    private static readonly (SkillType Skill, int Count)[] Plan =
    {
        (SkillType.Grammar, 4), (SkillType.Reading, 2), (SkillType.Listening, 2),
        (SkillType.Vocabulary, 1), (SkillType.Writing, 1), (SkillType.Speaking, 1),
    };

    public static async Task GenerateAsync(IServiceProvider services, int perLevel = 5, string[]? levelCodes = null)
    {
        levelCodes ??= new[] { "A1", "A2", "B1" };
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var codeRaw in levelCodes)
        {
            if (!Enum.TryParse<CefrLevel>(codeRaw, ignoreCase: true, out var cefr))
            {
                Console.WriteLine($"{codeRaw}: not a CEFR level — skipped."); continue;
            }
            var code = cefr.ToString();
            var level = await db.Levels.Include(l => l.PracticeSets).FirstOrDefaultAsync(l => l.Code == cefr);
            if (level is null) { Console.WriteLine($"{code}: level not found — skipped."); continue; }

            var existing = level.PracticeSets.Count(p => p.Title.Contains(Marker));
            if (existing >= perLevel) { Console.WriteLine($"{code}: already has {existing} {Marker} sets — skipped."); continue; }

            var pools = (await db.Exercises.AsNoTracking()
                    .Where(e => e.Lesson!.Unit!.LevelId == level.Id)
                    .Select(e => new { e.Id, e.Skill }).ToListAsync())
                .GroupBy(e => e.Skill)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

            for (int n = existing + 1; n <= perLevel; n++)
            {
                var itemIds = SelectBalanced(pools, n);
                if (itemIds.Count == 0) { Console.WriteLine($"{code}: no exercises to build a set — skipped."); break; }

                var set = new PracticeSet
                {
                    LevelId = level.Id,
                    Title = $"{code} {Marker} {n}",
                    Description = $"Vollständiger Übungssatz {n} für {code}: Grammatik, Lesen, Hören, Schreiben und Sprechen.",
                    Skill = null,
                    Kind = "exam",
                    IsExam = true,
                    TimeLimitMinutes = TimeLimit(code),
                    Order = level.PracticeSets.Count + 1,
                };
                var o = 1;
                foreach (var id in itemIds) set.Items.Add(new PracticeSetItem { ExerciseId = id, Order = o++ });
                db.PracticeSets.Add(set);
                level.PracticeSets.Add(set);
                Console.WriteLine($"{code}: created '{set.Title}' with {itemIds.Count} exercises.");
            }
        }
        await db.SaveChangesAsync();
    }

    // Pick a balanced mix; the set index offsets each skill's slice so the 5 sets differ.
    private static List<int> SelectBalanced(Dictionary<SkillType, List<int>> pools, int setIndex)
    {
        var picks = new List<int>();
        foreach (var (skill, count) in Plan)
        {
            if (!pools.TryGetValue(skill, out var pool) || pool.Count == 0) continue;
            for (var i = 0; i < count; i++)
            {
                var idx = ((setIndex - 1) * count + i) % pool.Count;
                picks.Add(pool[idx]);
            }
        }
        return picks.Distinct().ToList();
    }

    private static int TimeLimit(string code) => code switch { "A1" => 65, "A2" => 80, "B1" => 150, _ => 90 };
}
