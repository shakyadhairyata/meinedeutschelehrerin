using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

public record SetFeatureRequest(bool Enabled);

/// <summary>Admin-only operations. Requires the caller to be in the Admin role.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IFeatureFlagService _flags;
    public AdminController(IFeatureFlagService flags) => _flags = flags;

    [HttpGet("features")]
    public async Task<IActionResult> Features(CancellationToken ct) => Ok(await _flags.GetAllAsync(ct));

    [HttpPut("features/{key}")]
    public async Task<IActionResult> SetFeature(string key, [FromBody] SetFeatureRequest req, CancellationToken ct)
    {
        var flag = await _flags.SetAsync(key, req.Enabled, ct);
        return flag is null ? NotFound() : Ok(flag);
    }
}
