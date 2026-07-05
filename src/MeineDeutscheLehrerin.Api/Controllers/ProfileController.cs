using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Infrastructure.Identity;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Api.Controllers;

public record ProfileDto(string Email, string DisplayName, CefrLevel? TargetLevel,
    string TimeZoneId, int CurrentStreak, int LongestStreak, DateTimeOffset CreatedAt, bool IsAdmin,
    string Tier, int AiUsageToday, int AiDailyLimit);

public record UpdateProfileRequest(string? DisplayName, CefrLevel? TargetLevel, string? TimeZoneId);

[Route("api/profile")]
public class ProfileController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAiAccessService _aiAccess;

    public ProfileController(UserManager<ApplicationUser> users, IAiAccessService aiAccess)
    {
        _users = users;
        _aiAccess = aiAccess;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();
        return Ok(await ToDtoAsync(user));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest req)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        if (req.DisplayName is not null) user.DisplayName = req.DisplayName.Trim();
        if (req.TargetLevel is not null) user.TargetLevel = req.TargetLevel;
        if (!string.IsNullOrWhiteSpace(req.TimeZoneId)) user.TimeZoneId = req.TimeZoneId.Trim();

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);
        return Ok(await ToDtoAsync(user));
    }

    private async Task<ProfileDto> ToDtoAsync(ApplicationUser u)
    {
        var isAdmin = await _users.IsInRoleAsync(u, "Admin");
        var ai = await _aiAccess.GetStatusAsync(u.Id);
        return new(
            u.Email ?? "", string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? "").Split('@')[0] : u.DisplayName,
            u.TargetLevel, u.TimeZoneId, u.CurrentStreak, u.LongestStreak, u.CreatedAt, isAdmin,
            ai.Tier, ai.UsageToday, ai.DailyLimit);
    }
}
