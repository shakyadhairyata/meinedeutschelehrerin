using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;

namespace MeineDeutscheLehrerin.Infrastructure.Services;

/// <summary>
/// Client for the multi-agent Study Coach in the language-service. The coach itself runs the
/// LangGraph orchestration and holds the per-thread memory; this class just forwards a turn and
/// shapes the response for the SPA, stripping the exercise answer key (the coach grades
/// server-side from its own memory, so the client must never receive the solution).
/// </summary>
public interface ICoachService
{
    Task<CoachTurnDto?> TurnAsync(string userId, string message, CefrLevel? level, string? goal,
        JsonNode? submission, string? threadId, bool allowAi, CancellationToken ct = default);
}

public class CoachService : ICoachService
{
    private readonly HttpClient _http;
    private readonly ILogger<CoachService> _log;

    public CoachService(HttpClient http, ILogger<CoachService> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<CoachTurnDto?> TurnAsync(string userId, string message, CefrLevel? level,
        string? goal, JsonNode? submission, string? threadId, bool allowAi, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/coach/turn", new
            {
                user_id = userId,
                message,
                level = level?.ToString(),
                goal,
                submission,
                thread_id = threadId,
                allow_ai = allowAi,
            }, ct);
            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadFromJsonAsync<CoachRaw>(cancellationToken: ct);
            if (raw is null) return null;

            var exercise = raw.Exercise;
            exercise?.AsObject().Remove("solution"); // never leak the answer key to the client

            var steps = (raw.Steps ?? new())
                .Select(s => new CoachStepDto(s.Agent ?? "", s.Tool, s.Topic, s.Message))
                .ToList();

            var metrics = raw.Metrics is null ? null : new CoachMetricsDto(
                raw.Metrics.LatencyMs, raw.Metrics.LlmCalls, raw.Metrics.TotalTokens,
                raw.Metrics.PromptVersions ?? new(), raw.Metrics.Tracing);

            return new CoachTurnDto(
                raw.Reply ?? "", raw.Plan ?? new(), steps, exercise, raw.Evaluation,
                raw.WeakTopics ?? new(), raw.ThreadId ?? threadId ?? userId,
                allowAi && (raw.Metrics?.LlmCalls ?? 0) > 0, metrics);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Study coach turn failed (is the language-service running?).");
            return null;
        }
    }

    // Mirrors the language-service JSON (snake/camel handled by the Web defaults).
    private sealed class CoachRaw
    {
        public string? Reply { get; set; }
        public List<string>? Plan { get; set; }
        public List<StepRaw>? Steps { get; set; }
        public JsonNode? Exercise { get; set; }
        public JsonNode? Evaluation { get; set; }
        public List<string>? WeakTopics { get; set; }
        public string? ThreadId { get; set; }
        public MetricsRaw? Metrics { get; set; }
    }

    private sealed class StepRaw
    {
        public string? Agent { get; set; }
        public string? Tool { get; set; }
        public string? Topic { get; set; }
        public string? Message { get; set; }
    }

    private sealed class MetricsRaw
    {
        public double LatencyMs { get; set; }
        public int LlmCalls { get; set; }
        public int TotalTokens { get; set; }
        public List<string>? PromptVersions { get; set; }
        public bool Tracing { get; set; }
    }
}
