using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Api.Tools;

/// <summary>
/// Assembles authentic Goethe-format mock exams from a level's existing exercises: the four
/// modules — Lesen, Hören, Schreiben, Sprechen — each separately timed to the official durations.
/// Idempotent by title, so re-running only tops a level up to the requested count.
/// </summary>
public static class PracticeSetGenerator
{
    private const string Marker = "Prüfungssimulation";

    // Official Goethe module structure per level: (module, skill, minutes, target item count).
    private static readonly Dictionary<string, (string Title, SkillType Skill, int Minutes, int Items)[]> Plan = new()
    {
        ["A1"] = new[]
        {
            ("Lesen", SkillType.Reading, 25, 5), ("Hören", SkillType.Listening, 20, 4),
            ("Schreiben", SkillType.Writing, 20, 2), ("Sprechen", SkillType.Speaking, 15, 2),
        },
        ["A2"] = new[]
        {
            ("Lesen", SkillType.Reading, 30, 5), ("Hören", SkillType.Listening, 30, 4),
            ("Schreiben", SkillType.Writing, 30, 2), ("Sprechen", SkillType.Speaking, 15, 3),
        },
        ["B1"] = new[]
        {
            ("Lesen", SkillType.Reading, 65, 5), ("Hören", SkillType.Listening, 40, 4),
            ("Schreiben", SkillType.Writing, 60, 3), ("Sprechen", SkillType.Speaking, 15, 3),
        },
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
            if (!Plan.TryGetValue(code, out var plan)) { Console.WriteLine($"{code}: no module plan — skipped."); continue; }

            var level = await db.Levels.Include(l => l.PracticeSets).FirstOrDefaultAsync(l => l.Code == cefr);
            if (level is null) { Console.WriteLine($"{code}: level not found — skipped."); continue; }

            var existing = level.PracticeSets.Count(p => p.Title.Contains(Marker));
            if (existing >= perLevel) { Console.WriteLine($"{code}: already has {existing} exams — skipped."); continue; }

            var pool = (await db.Exercises.AsNoTracking()
                    .Where(e => e.Lesson!.Unit!.LevelId == level.Id)
                    .Select(e => new { e.Id, e.Skill }).ToListAsync())
                .GroupBy(e => e.Skill).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

            for (var n = existing + 1; n <= perLevel; n++)
            {
                var set = new PracticeSet
                {
                    LevelId = level.Id,
                    Title = $"{code} {Marker} {n}",
                    Description = $"Vollständige Goethe-Modellprüfung {n} für {code}: Lesen, Hören, Schreiben und Sprechen — jedes Modul mit eigenem Zeitlimit.",
                    Skill = null, Kind = "exam", IsExam = true,
                    TimeLimitMinutes = plan.Sum(m => m.Minutes),
                    Order = level.PracticeSets.Count + 1,
                };

                var mOrder = 1;
                var anyItems = false;
                foreach (var (title, skill, minutes, items) in plan)
                {
                    var module = new PracticeSetModule { Title = title, Skill = skill, TimeLimitMinutes = minutes, Order = mOrder++ };
                    if (pool.TryGetValue(skill, out var ids) && ids.Count > 0)
                    {
                        var chosen = new List<int>();
                        for (var i = 0; i < items; i++)
                        {
                            var id = ids[((n - 1) * items + i) % ids.Count];
                            if (!chosen.Contains(id)) chosen.Add(id);
                        }
                        var io = 1;
                        foreach (var id in chosen) module.Items.Add(new PracticeSetModuleItem { ExerciseId = id, Order = io++ });
                        anyItems |= module.Items.Count > 0;
                    }
                    set.Modules.Add(module);
                }

                if (!anyItems) { Console.WriteLine($"{code}: no exercises to fill modules — skipped."); break; }
                db.PracticeSets.Add(set);
                level.PracticeSets.Add(set);
                Console.WriteLine($"{code}: created '{set.Title}' ({set.Modules.Count} modules, {set.Modules.Sum(m => m.Items.Count)} exercises).");
            }
        }
        await db.SaveChangesAsync();
    }
}
