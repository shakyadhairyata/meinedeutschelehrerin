using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

public class LanguageServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8001";
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Evaluates free-form Writing &amp; Speaking via the Python language-service (Claude).
/// If the service is unreachable, every call falls back to a deterministic local heuristic,
/// so the app stays fully usable with no Python service and no API key.
/// </summary>
public interface ILanguageService
{
    Task<WritingFeedbackDto> EvaluateWritingAsync(string prompt, string text, CefrLevel level, int minWords, CancellationToken ct = default);
    Task<SpeakingFeedbackDto> EvaluateSpeakingAsync(string targetText, string transcript, CefrLevel level, CancellationToken ct = default);
    Task<GeneratedVocabularyDto> GenerateVocabularyAsync(CefrLevel level, string? theme, int count, IEnumerable<string> exclude, CancellationToken ct = default);
}

public class LanguageServiceClient : ILanguageService
{
    private readonly HttpClient _http;
    private readonly ILogger<LanguageServiceClient> _log;

    public LanguageServiceClient(HttpClient http, ILogger<LanguageServiceClient> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<WritingFeedbackDto> EvaluateWritingAsync(string prompt, string text, CefrLevel level, int minWords, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/evaluate/writing",
                new { prompt, text, level = level.ToString(), min_words = minWords }, ct);
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<WritingFeedbackDto>(cancellationToken: ct);
            if (dto is not null) return dto;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Language-service writing eval failed; using offline fallback.");
        }
        return OfflineWriting(text, minWords, level);
    }

    public async Task<SpeakingFeedbackDto> EvaluateSpeakingAsync(string targetText, string transcript, CefrLevel level, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/evaluate/speaking",
                new { target_text = targetText, transcript, level = level.ToString() }, ct);
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<SpeakingFeedbackDto>(cancellationToken: ct);
            if (dto is not null) return dto;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Language-service speaking eval failed; using offline fallback.");
        }
        return OfflineSpeaking(targetText, transcript);
    }

    public async Task<GeneratedVocabularyDto> GenerateVocabularyAsync(
        CefrLevel level, string? theme, int count, IEnumerable<string> exclude, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/generate/vocabulary",
                new { level = level.ToString(), theme, count, exclude = exclude.ToArray() }, ct);
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<GeneratedVocabularyDto>(cancellationToken: ct);
            if (dto is not null) return dto;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Vocabulary generation failed (is the language-service running?).");
        }
        return new GeneratedVocabularyDto(Array.Empty<GeneratedVocabItemDto>());
    }

    // ---- deterministic offline fallbacks ----

    private static WritingFeedbackDto OfflineWriting(string text, int minWords, CefrLevel level)
    {
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int wc = words.Length;
        double lengthScore = minWords <= 0 ? 1 : Math.Clamp((double)wc / minWords, 0, 1);
        bool capitalised = text.Length == 0 || char.IsUpper(text.TrimStart().FirstOrDefault('a'));
        bool punctuated = text.TrimEnd().EndsWith('.') || text.TrimEnd().EndsWith('!') || text.TrimEnd().EndsWith('?');
        double score = Math.Round(100 * (0.6 * lengthScore + (capitalised ? 0.2 : 0) + (punctuated ? 0.2 : 0)), 1);

        var strengths = new List<string>();
        if (wc >= minWords) strengths.Add($"Reached the length target ({wc}/{minWords} words).");
        if (capitalised) strengths.Add("Sentences start with a capital letter.");
        if (punctuated) strengths.Add("Text ends with sentence punctuation.");
        if (strengths.Count == 0) strengths.Add("You produced text — keep building on it.");

        var corrections = new List<WritingCorrectionDto>();
        if (wc < minWords)
            corrections.Add(new("(too short)", $"Write at least {minWords} words.",
                "Develop your ideas with more detail and examples.", "length"));

        return new WritingFeedbackDto(
            score,
            "Offline heuristic review (start the language-service for full AI feedback).",
            strengths, corrections, text, level.ToString());
    }

    private static SpeakingFeedbackDto OfflineSpeaking(string targetText, string transcript)
    {
        static string Norm(string s) => string.Join(' ',
            s.ToLowerInvariant().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries));
        var t = Norm(targetText).Split(' ');
        var h = Norm(transcript).Split(' ');
        int match = h.Count(w => t.Contains(w));
        double acc = t.Length == 0 ? 0 : Math.Round(100.0 * match / t.Length, 1);
        return new SpeakingFeedbackDto(
            acc, transcript,
            "Offline heuristic review (start the language-service for full pronunciation feedback).",
            new[] { "Speak slowly and clearly.", "Stress the first syllable of separable-prefix verbs." },
            acc);
    }
}
