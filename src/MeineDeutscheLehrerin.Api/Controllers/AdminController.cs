using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Domain.Entities;
using MeineDeutscheLehrerin.Infrastructure.Identity;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

public record SetFeatureRequest(bool Enabled);
public record SetTierRequest(SubscriptionTier Tier);
public record AdminUserDto(string Id, string Email, string DisplayName, string Tier, bool IsAdmin,
    int AiUsageToday, DateTimeOffset CreatedAt);

/// <summary>Admin-only operations. Requires the caller to be in the Admin role.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IFeatureFlagService _flags;
    private readonly UserManager<ApplicationUser> _users;

    public AdminController(IFeatureFlagService flags, UserManager<ApplicationUser> users)
    {
        _flags = flags;
        _users = users;
    }

    [HttpGet("features")]
    public async Task<IActionResult> Features(CancellationToken ct) => Ok(await _flags.GetAllAsync(ct));

    [HttpPut("features/{key}")]
    public async Task<IActionResult> SetFeature(string key, [FromBody] SetFeatureRequest req, CancellationToken ct)
    {
        var flag = await _flags.SetAsync(key, req.Enabled, ct);
        return flag is null ? NotFound() : Ok(flag);
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var admins = (await _users.GetUsersInRoleAsync("Admin")).Select(u => u.Id).ToHashSet();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // SQLite can't ORDER BY a DateTimeOffset column, so sort the loaded list in memory.
        var users = await _users.Users.ToListAsync(ct);
        return Ok(users.OrderByDescending(u => u.CreatedAt).Select(u => new AdminUserDto(
            u.Id, u.Email ?? "", u.DisplayName, u.Tier.ToString(), admins.Contains(u.Id),
            u.AiUsageDate == today ? u.AiUsageCount : 0, u.CreatedAt)));
    }

    [HttpPut("users/{id}/tier")]
    public async Task<IActionResult> SetTier(string id, [FromBody] SetTierRequest req, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        user.Tier = req.Tier;
        var result = await _users.UpdateAsync(user);
        return result.Succeeded ? Ok(new { user.Id, Tier = user.Tier.ToString() }) : BadRequest(result.Errors);
    }
}
