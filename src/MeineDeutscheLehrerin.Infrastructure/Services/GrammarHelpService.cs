using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Data;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

/// <summary>
/// Grammar help backed by retrieval over the app's <em>own</em> curriculum — lesson
/// explanations and exercise rationales — rather than the model's general knowledge, so an
/// answer can always be traced to material the learner has actually been taught.
///
/// The vector index lives in the Python language-service; this class owns the corpus (it has
/// the curriculum database) and pushes it over for indexing. Retrieval is free; the optional
/// Claude answer is quota-gated by the caller.
/// </summary>
public interface IGrammarHelpService
{
    Task<GrammarHelpDto> AskAsync(string query, CefrLevel? level, int k, bool withAnswer, CancellationToken ct = default);
    Task<RagIndexResultDto> ReindexAsync(CancellationToken ct = default);
}

public class GrammarHelpService : IGrammarHelpService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<GrammarHelpService> _log;

    public GrammarHelpService(AppDbContext db, HttpClient http, ILogger<GrammarHelpService> log)
    {
        _db = db;
        _http = http;
        _log = log;
    }

    public async Task<GrammarHelpDto> AskAsync(
        string query, CefrLevel? level, int k, bool withAnswer, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/rag/grammar", new
            {
                query,
                level = level?.ToString(),
                k,
                with_answer = withAnswer,
            }, ct);
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<RagResponse>(cancellationToken: ct);
            if (dto is not null)
                return new GrammarHelpDto(
                    query,
                    dto.Sources.Select(s => new GrammarSourceDto(
                        s.Title, s.GrammarTopic, s.Level, s.Source, s.Text, s.Score)).ToList(),
                    dto.Answer, dto.Grounded, dto.Retrieval, withAnswer && dto.Answer is not null);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Grammar retrieval failed (is the language-service running?).");
        }
        // Stay usable with no language-service: the caller renders an empty-state instead of erroring.
        return new GrammarHelpDto(query, Array.Empty<GrammarSourceDto>(), null, false, "unavailable", false);
    }

    public async Task<RagIndexResultDto> ReindexAsync(CancellationToken ct = default)
    {
        var docs = await BuildCorpusAsync(ct);
        var resp = await _http.PostAsJsonAsync("/rag/index", new { docs }, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<RagIndexResultDto>(cancellationToken: ct);
        _log.LogInformation("Grammar index rebuilt from {Count} documents.", docs.Count);
        return result ?? new RagIndexResultDto(0, "unknown", 0, "");
    }

    /// <summary>
    /// The teaching corpus: lesson bodies (the grammar explanations) plus per-exercise
    /// rationales. Standalone exam items have no lesson and are deliberately excluded —
    /// they are assessment, not instruction.
    /// </summary>
    private async Task<List<RagDoc>> BuildCorpusAsync(CancellationToken ct)
    {
        var lessons = await _db.Lessons.AsNoTracking()
            .Where(l => l.Content != "")
            .Select(l => new RagDoc(
                l.Unit!.Level!.Code.ToString(), "lesson", l.Title, l.GrammarTopic, l.Content))
            .ToListAsync(ct);

        var exercises = await _db.Exercises.AsNoTracking()
            .Where(e => e.LessonId != null && e.Explanation != "")
            .Select(e => new RagDoc(
                e.Lesson!.Unit!.Level!.Code.ToString(), "exercise",
                e.Lesson!.Title, e.GrammarTopic ?? e.Lesson!.GrammarTopic,
                e.Prompt + "\n" + e.Explanation))
            .ToListAsync(ct);

        return lessons.Concat(exercises).ToList();
    }

    public record RagDoc(string Level, string Source, string Title, string? GrammarTopic, string Text);

    private record RagSource(string Title, string? GrammarTopic, string Level, string Source, string Text, double Score);

    private record RagResponse(
        string Query, List<RagSource> Sources, string? Answer, bool Grounded, string Retrieval);
}
