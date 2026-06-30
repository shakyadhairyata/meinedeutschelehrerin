using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public record GradeOutcome(bool IsCorrect, double ScorePercent, JsonNode? CorrectAnswer);

public interface IExerciseGrader
{
    bool CanAutoGrade(ExerciseType type);
    GradeOutcome Grade(Exercise exercise, JsonNode? response);
}

/// <summary>
/// Deterministic, server-side grader for auto-gradable exercise types.
///
/// JSON schemas (Content / Solution / Response):
///  MultipleChoice / Reading / ListeningComprehension:
///     Content  { "question", "options":[..], "audioText"? }   Solution { "correctIndex":n }   Response { "selectedIndex":n }
///  FillInBlank / Cloze:
///     Content  { "text":"Ich ___ ...", "blanks":[{"hint"}] }   Solution { "answers":[["bin","war"], ...] }   Response { "answers":["bin", ...] }
///  Reorder:
///     Content  { "tokens":[..shuffled..] }   Solution { "answer":"Ich bin Student" }   Response { "tokens":[..] } | { "text":".." }
///  Matching:
///     Content  { "left":[..], "right":[..] }   Solution { "pairs":[[l,r], ..] }   Response { "pairs":[[l,r], ..] }
///  Dictation / Translation:
///     Content  { "audioText"? | "source" }   Solution { "answers":["..", ".."] } | { "text":".." }   Response { "text":".." }   (fuzzy)
///  Conjugation:
///     Content  { "verb","person","tense" }   Solution { "answers":["bin"] }   Response { "answer":"bin" }
/// </summary>
public class ExerciseGrader : IExerciseGrader
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public bool CanAutoGrade(ExerciseType type) => type.IsAutoGradable();

    public GradeOutcome Grade(Exercise exercise, JsonNode? response)
    {
        var solution = Parse(exercise.SolutionJson);
        return exercise.Type switch
        {
            ExerciseType.MultipleChoice or ExerciseType.ReadingComprehension
                or ExerciseType.ListeningComprehension => GradeChoice(solution, response),
            ExerciseType.FillInBlank or ExerciseType.Cloze => GradeBlanks(solution, response),
            ExerciseType.Reorder => GradeReorder(solution, response),
            ExerciseType.Matching => GradeMatching(solution, response),
            ExerciseType.Conjugation => GradeTextAnswers(solution, response, fuzzy: false),
            ExerciseType.Dictation or ExerciseType.Translation => GradeTextAnswers(solution, response, fuzzy: true),
            _ => new GradeOutcome(false, 0, solution)
        };
    }

    private static JsonNode? Parse(string json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    private static GradeOutcome GradeChoice(JsonNode? solution, JsonNode? response)
    {
        var correct = solution?["correctIndex"]?.GetValue<int>() ?? -1;
        var picked = response?["selectedIndex"]?.GetValue<int?>() ?? -2;
        var ok = correct >= 0 && correct == picked;
        return new GradeOutcome(ok, ok ? 100 : 0, JsonValue.Create(correct));
    }

    private static GradeOutcome GradeBlanks(JsonNode? solution, JsonNode? response)
    {
        var key = solution?["answers"] as JsonArray;
        var given = response?["answers"] as JsonArray;
        if (key is null || key.Count == 0) return new GradeOutcome(false, 0, solution?["answers"]);

        int correct = 0;
        for (int i = 0; i < key.Count; i++)
        {
            var acceptable = (key[i] as JsonArray)?.Select(n => n!.GetValue<string>()) ?? Enumerable.Empty<string>();
            var answer = given is not null && i < given.Count ? given[i]?.GetValue<string>() : null;
            if (answer is not null && acceptable.Any(a => Normalize(a) == Normalize(answer))) correct++;
        }
        var score = 100.0 * correct / key.Count;
        return new GradeOutcome(correct == key.Count, score, solution?["answers"]);
    }

    private static GradeOutcome GradeReorder(JsonNode? solution, JsonNode? response)
    {
        var expected = solution?["answer"]?.GetValue<string>() ?? "";
        string given = response?["text"]?.GetValue<string>()
            ?? string.Join(" ", (response?["tokens"] as JsonArray)?.Select(n => n!.GetValue<string>()) ?? Enumerable.Empty<string>());
        var ok = NormalizeSentence(expected) == NormalizeSentence(given);
        return new GradeOutcome(ok, ok ? 100 : 0, JsonValue.Create(expected));
    }

    private static GradeOutcome GradeMatching(JsonNode? solution, JsonNode? response)
    {
        var key = ToPairSet(solution?["pairs"] as JsonArray);
        var given = ToPairSet(response?["pairs"] as JsonArray);
        if (key.Count == 0) return new GradeOutcome(false, 0, solution?["pairs"]);
        int correct = given.Count(p => key.Contains(p));
        var score = 100.0 * correct / key.Count;
        return new GradeOutcome(correct == key.Count && given.Count == key.Count, score, solution?["pairs"]);
    }

    private static GradeOutcome GradeTextAnswers(JsonNode? solution, JsonNode? response, bool fuzzy)
    {
        var given = response?["text"]?.GetValue<string>() ?? response?["answer"]?.GetValue<string>() ?? "";
        var acceptable = new List<string>();
        if (solution?["answers"] is JsonArray arr) acceptable.AddRange(arr.Select(n => n!.GetValue<string>()));
        if (solution?["text"] is JsonValue tv) acceptable.Add(tv.GetValue<string>());
        if (acceptable.Count == 0) return new GradeOutcome(false, 0, solution);

        if (!fuzzy)
        {
            var ok = acceptable.Any(a => Normalize(a) == Normalize(given));
            return new GradeOutcome(ok, ok ? 100 : 0, JsonValue.Create(acceptable[0]));
        }

        double best = acceptable.Max(a => Similarity(NormalizeSentence(a), NormalizeSentence(given)));
        var score = Math.Round(best * 100, 1);
        return new GradeOutcome(best >= 0.95, score, JsonValue.Create(acceptable[0]));
    }

    private static HashSet<(int, int)> ToPairSet(JsonArray? pairs) =>
        pairs is null ? new()
        : pairs.OfType<JsonArray>().Where(p => p.Count == 2)
            .Select(p => (p[0]!.GetValue<int>(), p[1]!.GetValue<int>())).ToHashSet();

    // ---- text normalisation & fuzzy matching ----

    private static string Normalize(string s) =>
        s.Trim().ToLower(CultureInfo.GetCultureInfo("de-DE"));

    private static string NormalizeSentence(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!char.IsPunctuation(ch)) sb.Append(ch);
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLower(CultureInfo.GetCultureInfo("de-DE"));
    }

    /// <summary>Normalised Levenshtein similarity in [0,1].</summary>
    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        int dist = Levenshtein(a, b);
        return 1.0 - (double)dist / Math.Max(a.Length, b.Length);
    }

    private static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[a.Length, b.Length];
    }
}
