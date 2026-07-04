using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

/// <summary>Public feature map so the SPA can show/hide features. Sensitive gates are still
/// enforced server-side; this endpoint is only a UI hint.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/features")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureFlagService _flags;
    public FeaturesController(IFeatureFlagService flags) => _flags = flags;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _flags.GetMapAsync(ct));
}
