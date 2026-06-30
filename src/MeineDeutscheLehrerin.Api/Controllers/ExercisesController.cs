using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

[Route("api/exercises")]
public class ExercisesController : ApiControllerBase
{
    private readonly IProgressService _progress;
    public ExercisesController(IProgressService progress) => _progress = progress;

    /// <summary>Grade a single exercise attempt (auto-graded, or AI-scored for Writing/Speaking) and record it.</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitAttemptRequest req, CancellationToken ct) =>
        await _progress.SubmitAttemptAsync(UserId, req, ct) is { } result ? Ok(result) : NotFound();
}
