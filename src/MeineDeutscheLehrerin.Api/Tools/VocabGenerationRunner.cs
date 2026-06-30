using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Tools;

/// <summary>
/// CLI pipeline: generates vocabulary for a level via the language-service (Claude, or its
/// offline fallback), dedupes against existing words, and inserts the new ones.
/// Usage: dotnet run --project src/MeineDeutscheLehrerin.Api -- generate-vocab &lt;LEVEL&gt; [count] [theme]
/// </summary>
public static class VocabGenerationRunner
{
    public static async Task RunAsync(IServiceProvider services, string levelCode, int target, string? theme)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lang = scope.ServiceProvider.GetRequiredService<ILanguageService>();

        if (!Enum.TryParse<CefrLevel>(levelCode, ignoreCase: true, out var code))
        {
            Console.WriteLine($"Unknown level '{levelCode}'. Use A1, A2, B1, B2 or C1.");
            return;
        }

        var level = await db.Levels.FirstOrDefaultAsync(l => l.Code == code);
        if (level is null) { Console.WriteLine($"Level {code} is not seeded."); return; }

        var existing = (await db.VocabularyItems.Where(v => v.LevelId == level.Id)
                .Select(v => v.German).ToListAsync())
            .Select(g => g.Trim().ToLowerInvariant()).ToHashSet();

        Console.WriteLine($"Level {code}: {existing.Count} existing words. Target: +{target} new" +
                          (theme is null ? "." : $" (theme: {theme})."));

        int added = 0, round = 0;
        const int maxRounds = 12, batch = 40;

        while (added < target && round < maxRounds)
        {
            round++;
            var need = Math.Min(batch, target - added);
            var result = await lang.GenerateVocabularyAsync(code, theme, need + 10, existing);

            int beforeRound = added;
            foreach (var item in result.Items)
            {
                if (added >= target) break;
                var key = item.German.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(key) || existing.Contains(key)) continue;

                existing.Add(key);
                db.VocabularyItems.Add(new VocabularyItem
                {
                    LevelId = level.Id,
                    German = item.German.Trim(),
                    English = item.English,
                    PartOfSpeech = item.PartOfSpeech,
                    Article = item.Article,
                    Plural = item.Plural,
                    ExampleSentence = item.Example,
                    ThemeTag = string.IsNullOrWhiteSpace(item.Theme) ? "Allgemein" : item.Theme,
                });
                added++;
            }
            await db.SaveChangesAsync();
            Console.WriteLine($"  round {round}: +{added - beforeRound} new (running total +{added})");

            if (added == beforeRound)
            {
                Console.WriteLine("  No new words this round — stopping. " +
                                  "Set ANTHROPIC_API_KEY on the language-service for full Goethe-scale lists.");
                break;
            }
        }

        Console.WriteLine($"Done. Added {added} new word(s) to {code}. Level now has {existing.Count} words.");
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Imports a JSON wordlist (array of GeneratedVocabItemDto) into a level, deduped.</summary>
    public static async Task ImportAsync(IServiceProvider services, string levelCode, string path)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!Enum.TryParse<CefrLevel>(levelCode, ignoreCase: true, out var code))
        {
            Console.WriteLine($"Unknown level '{levelCode}'."); return;
        }
        var level = await db.Levels.FirstOrDefaultAsync(l => l.Code == code);
        if (level is null) { Console.WriteLine($"Level {code} is not seeded."); return; }
        if (!File.Exists(path)) { Console.WriteLine($"File not found: {path}"); return; }

        var items = JsonSerializer.Deserialize<List<GeneratedVocabItemDto>>(await File.ReadAllTextAsync(path), JsonOpts)
                    ?? new List<GeneratedVocabItemDto>();

        var existing = (await db.VocabularyItems.Where(v => v.LevelId == level.Id)
                .Select(v => v.German).ToListAsync())
            .Select(g => g.Trim().ToLowerInvariant()).ToHashSet();

        int added = 0;
        foreach (var item in items)
        {
            var key = item.German.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key) || existing.Contains(key)) continue;
            existing.Add(key);
            db.VocabularyItems.Add(new VocabularyItem
            {
                LevelId = level.Id, German = item.German.Trim(), English = item.English,
                PartOfSpeech = item.PartOfSpeech, Article = item.Article, Plural = item.Plural,
                ExampleSentence = item.Example, ThemeTag = string.IsNullOrWhiteSpace(item.Theme) ? "Allgemein" : item.Theme,
            });
            added++;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"{code}: imported {added} new word(s) from {Path.GetFileName(path)}. Level now has {existing.Count}.");
    }

    /// <summary>
    /// Imports every wordlist for each level from a directory: all files named like
    /// {code}*.json (e.g. A1.json, A1.batch2.json), so batches can be appended over time.
    /// </summary>
    public static async Task ImportAllAsync(IServiceProvider services, string dir)
    {
        foreach (var code in new[] { "A1", "A2", "B1", "B2", "C1" })
        {
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir, $"{code}*.json").OrderBy(f => f, StringComparer.Ordinal).ToList()
                : new List<string>();
            if (files.Count == 0) { Console.WriteLine($"{code}: no files in {dir} (skipped)."); continue; }
            foreach (var file in files) await ImportAsync(services, code, file);
        }
    }
}
