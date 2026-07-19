using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

/// <summary>
/// "Warum ist das falsch?" — grammar explanations retrieved from the app's own lessons.
///
/// Retrieval is always free: it costs no tokens, so every learner gets the cited source
/// material regardless of tier. Only the optional Claude-written answer spends budget, and
/// that goes through the same quota gate as the rest of the AI features — a Free or
/// over-quota user still gets the retrieved explanations, just without the generated summary.
/// </summary>
[ApiController]
[Authorize]
[Route("api/grammar-help")]
public class GrammarHelpController : ApiControllerBase
{
    private readonly IGrammarHelpService _grammar;
    private readonly IFeatureFlagService _flags;
    private readonly IAiAccessService _ai;

    public GrammarHelpController(IGrammarHelpService grammar, IFeatureFlagService flags, IAiAccessService ai)
    {
        _grammar = grammar;
        _flags = flags;
        _ai = ai;
    }

    [HttpGet]
    public async Task<IActionResult> Ask(
        [FromQuery] string q,
        [FromQuery] CefrLevel? level = null,
        [FromQuery] int k = 4,
        [FromQuery] bool answer = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { error = "q is required" });
        if (!await _flags.IsEnabledAsync(FeatureKeys.GrammarHelp, ct))
            return NotFound(new { error = "Grammatik-Hilfe ist derzeit deaktiviert." });

        // Only charge the quota when a generated answer was actually asked for.
        var withAnswer = answer && await _ai.TryConsumeAsync(UserId, ct);
        return Ok(await _grammar.AskAsync(q, level, Math.Clamp(k, 1, 10), withAnswer, ct));
    }

    /// <summary>Rebuilds the vector index from the current curriculum. Admin-only: it re-embeds
    /// the whole corpus, so it should be run on content changes rather than per request.</summary>
    [HttpPost("reindex")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reindex(CancellationToken ct) => Ok(await _grammar.ReindexAsync(ct));
}
