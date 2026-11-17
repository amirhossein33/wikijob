using InterviewHub.Api.Auth;
using InterviewHub.Business.Auth;
using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterviewHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserManager<AppUser> users, ITokenService tokens) : ControllerBase
{
    // Registration accepts only member roles; Admin is created from trusted server configuration.
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponse>> Register(RegisterRequest request)
    {
        if (request.Role is not (AppRoles.Candidate or AppRoles.Interviewer))
            return BadRequest(new { error = "Role must be Candidate or Interviewer." });

        var user = new AppUser { UserName = request.UserName, Email = request.Email };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        var roleResult = await users.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await users.DeleteAsync(user);
            return StatusCode(500, new { error = "Could not assign account role." });
        }

        return Ok(tokens.Create(user, [request.Role]));
    }

    // The same login endpoint works for either member role and for Admin.
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = request.UserNameOrEmail.Contains('@')
            ? await users.FindByEmailAsync(request.UserNameOrEmail)
            : await users.FindByNameAsync(request.UserNameOrEmail);

        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { error = "Invalid credentials." });

        var roles = await users.GetRolesAsync(user);
        return Ok(tokens.Create(user, roles.ToArray()));
    }

    // Adding a second member role preserves the existing account and login.
    [HttpPost("roles/{role}")]
    [Authorize(Roles = AppRoles.Candidate + "," + AppRoles.Interviewer + "," + AppRoles.Admin)]
    public async Task<IActionResult> AddMemberRole(string role)
    {
        if (role is not (AppRoles.Candidate or AppRoles.Interviewer))
            return BadRequest(new { error = "Role must be Candidate or Interviewer." });

        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (await users.IsInRoleAsync(user, role)) return NoContent();
        var result = await users.AddToRoleAsync(user, role);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(e => e.Description) });
    }

    // Current roles are read from Identity because a token may predate a role change.
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AccountResponse>> Me()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await users.GetRolesAsync(user);
        return Ok(new AccountResponse(user.Id, user.UserName!, user.Email!, roles.ToArray()));
    }

    // A concrete admin-only endpoint makes 403 behavior verifiable.
    [HttpGet("admin-check")]
    [Authorize(Roles = AppRoles.Admin)]
    public IActionResult AdminCheck() => Ok(new { access = "Admin" });
}
