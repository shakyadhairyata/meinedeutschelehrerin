using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Tools;

/// <summary>One importable exercise; locates its target lesson by level + grammarTopic (or lessonTitle).</summary>
public record ExerciseImport(
    string Level, string? GrammarTopic, string? LessonTitle,
    ExerciseType Type, SkillType Skill, string Prompt,
    JsonNode? Content, JsonNode? Solution, string Explanation,
    int Difficulty = 1, int Points = 10);

/// <summary>
/// CLI pipeline: imports extra exercises from JSON into existing lessons. Deduped by prompt,
/// and self-validating — each auto-gradable exercise's own answer key must grade 100% or it's
/// skipped. Append-friendly (no DB reset needed).
/// Usage: dotnet run --project src/MeineDeutscheLehrerin.Api -- import-exercises all ./content/exercises
/// </summary>
public static class ExerciseImportRunner
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
    }

    public static async Task ImportAsync(IServiceProvider services, string path)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var grader = scope.ServiceProvider.GetRequiredService<IExerciseGrader>();
        if (!File.Exists(path)) { Console.WriteLine($"File not found: {path}"); return; }

        var imports = JsonSerializer.Deserialize<List<ExerciseImport>>(await File.ReadAllTextAsync(path), JsonOpts)
                      ?? new List<ExerciseImport>();

        var lessonCache = new Dictionary<string, Lesson?>();
        int added = 0, dupes = 0, invalid = 0, notFound = 0;

        foreach (var imp in imports)
        {
            if (!Enum.TryParse<CefrLevel>(imp.Level, ignoreCase: true, out var code)) { notFound++; continue; }
            var key = $"{code}|{imp.GrammarTopic ?? imp.LessonTitle}";

            if (!lessonCache.TryGetValue(key, out var lesson))
            {
                var q = from l in db.Lessons.Include(l => l.Exercises)
                        join u in db.Units on l.UnitId equals u.Id
                        join lv in db.Levels on u.LevelId equals lv.Id
                        where lv.Code == code
                        select l;
                lesson = !string.IsNullOrWhiteSpace(imp.GrammarTopic)
                    ? await q.FirstOrDefaultAsync(l => l.GrammarTopic == imp.GrammarTopic)
                    : await q.FirstOrDefaultAsync(l => l.Title == imp.LessonTitle);
                lessonCache[key] = lesson;
            }
            if (lesson is null) { notFound++; Console.WriteLine($"  ! lesson not found: {key}"); continue; }

            if (lesson.Exercises.Any(e => e.Prompt == imp.Prompt)) { dupes++; continue; }

            var ex = new Exercise
            {
                LessonId = lesson.Id,
                Type = imp.Type, Skill = imp.Skill, Prompt = imp.Prompt,
                ContentJson = imp.Content?.ToJsonString() ?? "{}",
                SolutionJson = imp.Solution?.ToJsonString() ?? "{}",
                Explanation = imp.Explanation,
                GrammarTopic = imp.GrammarTopic ?? lesson.GrammarTopic,
                Points = imp.Points, Difficulty = imp.Difficulty,
                Order = (lesson.Exercises.Count == 0 ? 0 : lesson.Exercises.Max(e => e.Order)) + 1,
            };

            if (grader.CanAutoGrade(ex.Type))
            {
                var outcome = grader.Grade(ex, BuildCorrectResponse(ex.Type, JsonNode.Parse(ex.SolutionJson)!));
                if (!outcome.IsCorrect) { invalid++; Console.WriteLine($"  ! invalid answer key, skipped: \"{imp.Prompt}\""); continue; }
            }

            lesson.Exercises.Add(ex); // tracked lesson → EF inserts; keeps cache consistent for dedupe
            added++;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"{Path.GetFileName(path)}: +{added} exercises (dupes {dupes}, invalid {invalid}, lesson-not-found {notFound}).");
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
