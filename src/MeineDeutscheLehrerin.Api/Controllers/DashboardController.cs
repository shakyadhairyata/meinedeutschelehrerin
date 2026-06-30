using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

[Route("api/dashboard")]
public class DashboardController : ApiControllerBase
{
    private readonly IAnalyticsService _analytics;
    public DashboardController(IAnalyticsService analytics) => _analytics = analytics;

    /// <summary>Progress + weakness analysis: streaks, per-skill accuracy, weak grammar topics, activity, level progress.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _analytics.GetDashboardAsync(UserId, ct));
}
