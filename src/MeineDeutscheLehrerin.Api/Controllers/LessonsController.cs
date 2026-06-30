using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

[Route("api/lessons")]
public class LessonsController : ApiControllerBase
{
    private readonly IProgressService _progress;
    public LessonsController(IProgressService progress) => _progress = progress;

    /// <summary>Lesson content + exercises (no solutions) + this user's progress. Marks the lesson started.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        await _progress.GetLessonAsync(id, UserId, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteLessonRequest? body, CancellationToken ct)
    {
        var req = new CompleteLessonRequest(id, body?.TimeSpentSeconds ?? 0);
        return await _progress.CompleteLessonAsync(UserId, req, ct) ? Ok() : NotFound();
    }
}
