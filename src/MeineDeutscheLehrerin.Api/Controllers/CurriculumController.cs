using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

[Route("api")]
public class CurriculumController : ApiControllerBase
{
    private readonly ICurriculumService _curriculum;
    public CurriculumController(ICurriculumService curriculum) => _curriculum = curriculum;

    [HttpGet("levels")]
    public async Task<IActionResult> GetLevels(CancellationToken ct) =>
        Ok(await _curriculum.GetLevelsAsync(UserId, ct));

    [HttpGet("levels/{id:int}")]
    public async Task<IActionResult> GetLevel(int id, CancellationToken ct) =>
        await _curriculum.GetLevelAsync(id, UserId, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpGet("units/{id:int}")]
    public async Task<IActionResult> GetUnit(int id, CancellationToken ct) =>
        await _curriculum.GetUnitAsync(id, UserId, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpGet("levels/{levelId:int}/practice-sets")]
    public async Task<IActionResult> GetPracticeSets(int levelId, CancellationToken ct) =>
        Ok(await _curriculum.GetPracticeSetsAsync(levelId, ct));

    [HttpGet("practice-sets/{id:int}")]
    public async Task<IActionResult> GetPracticeSet(int id, CancellationToken ct) =>
        await _curriculum.GetPracticeSetAsync(id, ct) is { } dto ? Ok(dto) : NotFound();
}
