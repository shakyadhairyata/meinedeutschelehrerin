using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain.Contracts;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

[Route("api/study-plan")]
public class StudyPlanController : ApiControllerBase
{
    private readonly IStudyPlanService _plans;
    public StudyPlanController(IStudyPlanService plans) => _plans = plans;

    /// <summary>The user's active 2-week plan, or 204 if none yet.</summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct) =>
        await _plans.GetActivePlanAsync(UserId, ct) is { } dto ? Ok(dto) : NoContent();

    /// <summary>Generate a 2-week plan for a level (one unit per day), replacing any active plan.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudyPlanRequest req, CancellationToken ct) =>
        await _plans.CreatePlanAsync(UserId, req, ct) is { } dto ? Ok(dto) : BadRequest("Unknown level or no units.");
}
