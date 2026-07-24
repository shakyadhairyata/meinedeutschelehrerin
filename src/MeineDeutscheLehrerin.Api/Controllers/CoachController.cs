using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

/// <summary>
/// The multi-agent Study Coach: a planner + grammar/exercise/evaluator agents (LangGraph, in the
/// language-service) that coordinate over the app's own RAG, generation and grading tools and
/// remember the session per user.
///
/// Gated by the <c>study_coach</c> feature flag. LLM enhancement (intent refinement, a polished
/// reply) is billed: a Paid user within quota gets it, and everyone else still gets the fully
/// functional deterministic coach — retrieval, exercises and grading cost no tokens. The memory
/// thread is the user id, so the coach is per-user and can't be crossed by a client-supplied id.
/// </summary>
[ApiController]
[Authorize]
[Route("api/coach")]
public class CoachController : ApiControllerBase
{
    private readonly ICoachService _coach;
    private readonly IFeatureFlagService _flags;
    private readonly IAiAccessService _ai;

    public CoachController(ICoachService coach, IFeatureFlagService flags, IAiAccessService ai)
    {
        _coach = coach;
        _flags = flags;
        _ai = ai;
    }

    [HttpPost("turn")]
    public async Task<IActionResult> Turn([FromBody] CoachTurnRequest req, CancellationToken ct)
    {
        if (!await _flags.IsEnabledAsync(FeatureKeys.StudyCoach, ct))
            return NotFound(new { error = "Der Lern-Coach ist derzeit deaktiviert." });
        if (req is null || string.IsNullOrWhiteSpace(req.Message) && req.Submission is null)
            return BadRequest(new { error = "message or submission is required" });

        // Spend one AI credit to unlock LLM enhancement; without it the coach still runs.
        var allowAi = await _ai.TryConsumeAsync(UserId, ct);

        var result = await _coach.TurnAsync(
            UserId, req.Message ?? "", req.Level, req.Goal, req.Submission,
            threadId: UserId, allowAi: allowAi, ct);

        return result is null
            ? StatusCode(503, new { error = "Der Lern-Coach ist gerade nicht erreichbar." })
            : Ok(result);
    }
}
