using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MeineDeutscheLehrerin.Domain;
using MeineDeutscheLehrerin.Infrastructure.Identity;

namespace MeineDeutscheLehrerin.Api.Controllers;

public record ProfileDto(string Email, string DisplayName, CefrLevel? TargetLevel,
    string TimeZoneId, int CurrentStreak, int LongestStreak, DateTimeOffset CreatedAt);

public record UpdateProfileRequest(string? DisplayName, CefrLevel? TargetLevel, string? TimeZoneId);

[Route("api/profile")]
public class ProfileController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    public ProfileController(UserManager<ApplicationUser> users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();
        return Ok(ToDto(user));
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
        return result.Succeeded ? Ok(ToDto(user)) : BadRequest(result.Errors);
    }

    private static ProfileDto ToDto(ApplicationUser u) => new(
        u.Email ?? "", string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? "").Split('@')[0] : u.DisplayName,
        u.TargetLevel, u.TimeZoneId, u.CurrentStreak, u.LongestStreak, u.CreatedAt);
}
