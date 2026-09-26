using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

/// <summary>
/// Word lookup for hover/tap in a lesson. Matching a word against the vocabulary is always free
/// (no tokens), so every learner gets it regardless of tier. Only an unknown word triggers an AI
/// gloss, which goes through the same quota gate as the other AI features — and the found base form
/// is saved to the vocabulary, so the same word is a free vocab hit next time.
/// </summary>
[Route("api/lookup")]
public class LookupController : ApiControllerBase
{
    private readonly ILookupService _lookup;
    private readonly IAiAccessService _ai;

    public LookupController(ILookupService lookup, IAiAccessService ai)
    {
        _lookup = lookup;
        _ai = ai;
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(
        [FromQuery] string word,
        [FromQuery] CefrLevel level = CefrLevel.A1,
        [FromQuery] string? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word)) return BadRequest(new { error = "word is required" });

        // Free, offline, always: match the word against the existing vocabulary.
        var hit = await _lookup.MatchVocabAsync(word, ct);
        if (hit is not null) return Ok(hit);

        // Miss: only spend an AI credit if the tier/quota allows; otherwise report "no entry".
        if (await _ai.TryConsumeAsync(UserId, ct))
        {
            var glossed = await _lookup.GlossAndSaveAsync(word, context, level, ct);
            if (glossed is not null) return Ok(glossed);
        }

        return Ok(new WordLookupDto(word, false, "none", null, null, null, null, null, null, null));
    }
}
