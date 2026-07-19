using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Tools;

/// <summary>A full, hand-authored Goethe mock exam (one Modellsatz) with its four timed modules.</summary>
public record ExamManifest(string Level, string Title, string Description, List<ExamModuleDef> Modules);

/// <summary>One timed module (Lesen/Hören/Schreiben/Sprechen) with its own duration and ordered items.</summary>
public record ExamModuleDef(string Title, SkillType Skill, int Minutes, List<ExamItemDef> Items);

/// <summary>A single exam item; becomes a standalone <see cref="Exercise"/> (no owning lesson).</summary>
public record ExamItemDef(
    ExerciseType Type, SkillType Skill, string Prompt,
    JsonNode? Content, JsonNode? Solution, string Explanation,
    int Difficulty = 3, int Points = 10);

/// <summary>
/// Imports curated, full-length Goethe mock exams from JSON manifests. Every item is authored
/// (correct Teil structure and official item counts) and stored as a standalone exercise wired
/// into a <see cref="PracticeSetModule"/> — no auto-assembly, no reused items across sets.
/// Idempotent by exam title; auto-gradable items are self-validated (a broken answer key is skipped).
/// Usage: dotnet run --project src/MeineDeutscheLehrerin.Api -- import-exams all ./content/exams
/// </summary>
public static class ExamImportRunner
{
    private static readonly JsonSerializerOptions JsonOpts = BuildOpts();
    private static JsonSerializerOptions BuildOpts()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public static async Task ImportAllAsync(IServiceProvider services, string dir)
    {
        if (!Directory.Exists(dir)) { Console.WriteLine($"Directory not found: {dir}"); return; }
        foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            await ImportAsync(services, file);
        await RetireSupersededExamsAsync(services);
    }

    /// <summary>
    /// Retires exams the authored Modellsätze replace: the shallow auto-generated sets (title
    /// "Prüfungssimulation"), and the seeded starter demo ("… Modellprüfung") for any level that
    /// now has an authored "Modellsatz". Runs after import so the replacement already exists;
    /// idempotent — after removal there is nothing left to match.
    /// </summary>
    private static async Task RetireSupersededExamsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var authoredLevels = await db.PracticeSets
            .Where(p => p.Title.Contains("Modellsatz"))
            .Select(p => p.LevelId).Distinct().ToListAsync();

        var stale = await db.PracticeSets
            .Where(p => p.Title.Contains("Prüfungssimulation")
                     || (p.Title.Contains("Modellprüfung") && authoredLevels.Contains(p.LevelId)))
            .ToListAsync();
        if (stale.Count == 0) return;
        db.PracticeSets.RemoveRange(stale);
        await db.SaveChangesAsync();
        Console.WriteLine($"Retired {stale.Count} superseded exam(s).");
    }

    public static async Task ImportAsync(IServiceProvider services, string path)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var grader = scope.ServiceProvider.GetRequiredService<IExerciseGrader>();
        if (!File.Exists(path)) { Console.WriteLine($"File not found: {path}"); return; }

        var manifest = JsonSerializer.Deserialize<ExamManifest>(await File.ReadAllTextAsync(path), JsonOpts);
        if (manifest is null) { Console.WriteLine($"{Path.GetFileName(path)}: empty/invalid manifest — skipped."); return; }

        if (!Enum.TryParse<CefrLevel>(manifest.Level, ignoreCase: true, out var code))
        { Console.WriteLine($"{Path.GetFileName(path)}: unknown level '{manifest.Level}' — skipped."); return; }

        var level = await db.Levels.FirstOrDefaultAsync(l => l.Code == code);
        if (level is null) { Console.WriteLine($"{manifest.Level}: level not found — skipped."); return; }

        // Idempotent by title. A previously-imported curated exam (has modules) is left untouched;
        // an older non-modular set with the same title (e.g. an auto-generated draft) is replaced.
        var existing = await db.PracticeSets.Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.LevelId == level.Id && p.Title == manifest.Title);
        if (existing is not null)
        {
            if (existing.Modules.Count > 0) { Console.WriteLine($"{manifest.Title}: already imported — skipped."); return; }
            db.PracticeSets.Remove(existing);
            await db.SaveChangesAsync();
            Console.WriteLine($"{manifest.Title}: replaced a previous non-modular set.");
        }

        var set = new PracticeSet
        {
            LevelId = level.Id,
            Title = manifest.Title,
            Description = manifest.Description,
            Skill = null,
            Kind = "exam",
            IsExam = true,
            TimeLimitMinutes = manifest.Modules.Sum(m => m.Minutes),
            Order = await db.PracticeSets.CountAsync(p => p.LevelId == level.Id) + 1,
        };

        int items = 0, invalid = 0;
        var mOrder = 1;
        foreach (var md in manifest.Modules)
        {
            var module = new PracticeSetModule { Title = md.Title, Skill = md.Skill, TimeLimitMinutes = md.Minutes, Order = mOrder++ };
            var iOrder = 1;
            foreach (var it in md.Items)
            {
                var ex = new Exercise
                {
                    LessonId = null,
                    Type = it.Type, Skill = it.Skill, Prompt = it.Prompt,
                    ContentJson = it.Content?.ToJsonString() ?? "{}",
                    SolutionJson = it.Solution?.ToJsonString() ?? "{}",
                    Explanation = it.Explanation,
                    Points = it.Points, Difficulty = it.Difficulty,
                    Order = iOrder,
                };

                if (grader.CanAutoGrade(ex.Type))
                {
                    var outcome = grader.Grade(ex, BuildCorrectResponse(ex.Type, JsonNode.Parse(ex.SolutionJson)!));
                    if (!outcome.IsCorrect) { invalid++; Console.WriteLine($"  ! invalid answer key, skipped: \"{it.Prompt}\""); continue; }
                }

                module.Items.Add(new PracticeSetModuleItem { Exercise = ex, Order = iOrder++ });
                items++;
            }
            set.Modules.Add(module);
        }

        db.PracticeSets.Add(set);
        await db.SaveChangesAsync();
        Console.WriteLine($"{manifest.Title}: created ({set.Modules.Count} modules, {items} items" + (invalid > 0 ? $", {invalid} invalid skipped" : "") + ").");
    }

    /// <summary>Canonical correct response from a solution, used to self-validate an answer key.</summary>
    private static JsonNode BuildCorrectResponse(ExerciseType type, JsonNode sol) => type switch
    {
        ExerciseType.MultipleChoice or ExerciseType.ReadingComprehension or ExerciseType.ListeningComprehension
            => new JsonObject { ["selectedIndex"] = sol["correctIndex"]!.GetValue<int>() },
        ExerciseType.FillInBlank or ExerciseType.Cloze
            => new JsonObject { ["answers"] = new JsonArray(((JsonArray)sol["answers"]!)
                .Select(a => (JsonNode)JsonValue.Create(((JsonArray)a!)[0]!.GetValue<string>())).ToArray()) },
        ExerciseType.Reorder => new JsonObject { ["text"] = sol["answer"]!.GetValue<string>() },
        ExerciseType.Matching => new JsonObject { ["pairs"] = sol["pairs"]!.DeepClone() },
        ExerciseType.Conjugation => new JsonObject { ["answer"] = ((JsonArray)sol["answers"]!)[0]!.GetValue<string>() },
        ExerciseType.Dictation or ExerciseType.Translation
            => new JsonObject { ["text"] = sol["text"]?.GetValue<string>() ?? ((JsonArray)sol["answers"]!)[0]!.GetValue<string>() },
        _ => new JsonObject(),
    };
}
