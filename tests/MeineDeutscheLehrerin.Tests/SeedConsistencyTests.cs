using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Data;
using MeineDeutscheLehrerin.Infrastructure.Seeding;
using MeineDeutscheLehrerin.Infrastructure.Services;
using Xunit;

namespace MeineDeutscheLehrerin.Tests;

/// <summary>
/// Validates the hand-authored curriculum: seeds it into an in-memory SQLite database and
/// checks that every auto-gradable exercise's own answer key grades as 100% correct.
/// This catches content authoring mistakes across all five levels.
/// </summary>
public class SeedConsistencyTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;
    private readonly ExerciseGrader _grader = new();

    public SeedConsistencyTests()
    {
        _conn = new SqliteConnection("Filename=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task Seeds_all_five_levels()
    {
        await DbSeeder.SeedAsync(_db);
        var codes = await _db.Levels.Select(l => l.Code).OrderBy(c => c).ToListAsync();
        Assert.Equal(new[] { CefrLevel.A1, CefrLevel.A2, CefrLevel.B1, CefrLevel.B2, CefrLevel.C1 }, codes);
    }

    [Fact]
    public async Task Seeding_is_idempotent()
    {
        await DbSeeder.SeedAsync(_db);
        var first = await _db.Levels.CountAsync();
        await DbSeeder.SeedAsync(_db); // second run must not duplicate
        var second = await _db.Levels.CountAsync();
        Assert.Equal(first, second);
        Assert.Equal(5, second);
    }

    [Fact]
    public async Task Every_lesson_has_a_grammar_topic_or_skill_and_some_content()
    {
        await DbSeeder.SeedAsync(_db);
        var lessons = await _db.Lessons.ToListAsync();
        Assert.NotEmpty(lessons);
        Assert.All(lessons, l => Assert.False(string.IsNullOrWhiteSpace(l.Content)));
    }

    [Fact]
    public async Task Every_autogradable_exercise_answer_key_scores_100()
    {
        await DbSeeder.SeedAsync(_db);
        var exercises = await _db.Exercises.ToListAsync();
        Assert.NotEmpty(exercises);

        var failures = new List<string>();
        foreach (var ex in exercises.Where(e => e.Type.IsAutoGradable()))
        {
            var response = BuildCorrectResponse(ex);
            var outcome = _grader.Grade(ex, response);
            if (!outcome.IsCorrect)
                failures.Add($"#{ex.Id} {ex.Type} [{ex.GrammarTopic}] \"{ex.Prompt}\" → {outcome.ScorePercent}%");
        }

        Assert.True(failures.Count == 0,
            "These authored exercises do not self-grade as correct:\n" + string.Join("\n", failures));
    }

    /// <summary>Builds the canonical correct response for an exercise from its SolutionJson.</summary>
    private static JsonNode BuildCorrectResponse(Exercise ex)
    {
        var sol = JsonNode.Parse(ex.SolutionJson)!;
        return ex.Type switch
        {
            ExerciseType.MultipleChoice or ExerciseType.ReadingComprehension or ExerciseType.ListeningComprehension
                => new JsonObject { ["selectedIndex"] = sol["correctIndex"]!.GetValue<int>() },

            ExerciseType.FillInBlank or ExerciseType.Cloze
                => new JsonObject
                {
                    ["answers"] = new JsonArray(((JsonArray)sol["answers"]!)
                        .Select(a => (JsonNode)JsonValue.Create(((JsonArray)a!)[0]!.GetValue<string>())).ToArray())
                },

            ExerciseType.Reorder
                => new JsonObject { ["text"] = sol["answer"]!.GetValue<string>() },

            ExerciseType.Matching
                => new JsonObject { ["pairs"] = sol["pairs"]!.DeepClone() },

            ExerciseType.Conjugation
                => new JsonObject { ["answer"] = ((JsonArray)sol["answers"]!)[0]!.GetValue<string>() },

            ExerciseType.Dictation or ExerciseType.Translation
                => new JsonObject { ["text"] = FirstText(sol) },

            _ => new JsonObject()
        };
    }

    private static string FirstText(JsonNode sol) =>
        sol["text"]?.GetValue<string>()
        ?? ((JsonArray)sol["answers"]!)[0]!.GetValue<string>();
}
