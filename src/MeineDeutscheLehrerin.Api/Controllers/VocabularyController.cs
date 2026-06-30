using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

[Route("api")]
public class VocabularyController : ApiControllerBase
{
    private readonly IVocabularyService _vocab;
    public VocabularyController(IVocabularyService vocab) => _vocab = vocab;

    [HttpGet("levels/{levelId:int}/vocabulary")]
    public async Task<IActionResult> GetDeck(int levelId, CancellationToken ct) =>
        Ok(await _vocab.GetDeckAsync(levelId, UserId, ct));

    /// <summary>The spaced-repetition queue: words due for review (new words first).</summary>
    [HttpGet("levels/{levelId:int}/vocabulary/due")]
    public async Task<IActionResult> GetDue(int levelId, [FromQuery] int limit = 20, CancellationToken ct = default) =>
        Ok(await _vocab.GetDueAsync(levelId, UserId, Math.Clamp(limit, 1, 100), ct));

    [HttpPost("vocabulary/review")]
    public async Task<IActionResult> Review([FromBody] VocabReviewRequest req, CancellationToken ct) =>
        await _vocab.ReviewAsync(UserId, req, ct) is { } dto ? Ok(dto) : NotFound();
}
