using System.Security.Claims;
using InterviewHub.Business.Auth;
using InterviewHub.Business.InterviewerExperiences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterviewHub.Api.Controllers;

[ApiController]
[Route("api/interviewer-experiences")]
public sealed class InterviewerExperiencesController(InterviewerExperienceService service) : ControllerBase
{
    // The interviewer role is required even when the account also has Candidate.
    [HttpPost]
    [Authorize(Roles = AppRoles.Interviewer + "," + AppRoles.Admin)]
    [EnableRateLimiting("submit")]
    public async Task<ActionResult<PrivateInterviewerExperienceResponse>> Create(
        CreateInterviewerExperienceRequest request, CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await service.CreateAsync(id, request, ct);
        return Ok(result);
    }

    // Public readers see only approved submissions and the permitted identity fields.
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicInterviewerExperienceResponse>> GetPublic(Guid id, CancellationToken ct)
    {
        var result = await service.GetPublicAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // The owner can review all submitted data regardless of public visibility.
    [HttpGet("{id:guid}/mine")]
    [Authorize]
    public async Task<ActionResult<PrivateInterviewerExperienceResponse>> GetMine(Guid id, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await service.GetPrivateAsync(id, userId, User.IsInRole(AppRoles.Admin), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // The interviewer dashboard receives private records, including moderation status.
    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Interviewer + "," + AppRoles.Admin)]
    public async Task<ActionResult<IReadOnlyList<PrivateInterviewerExperienceResponse>>> Mine(CancellationToken ct)
    {
        var authorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await service.MineAsync(authorId, ct));
    }

    // Full replacement returns edited content to the admin review queue.
    [HttpPut("{id:guid}/mine")]
    [Authorize(Roles = AppRoles.Interviewer)]
    public async Task<ActionResult<PrivateInterviewerExperienceResponse>> UpdateMine(
        Guid id, CreateInterviewerExperienceRequest request, CancellationToken ct)
    {
        var requesterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var item = await service.UpdateAsync(id, requesterId, false, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    // Soft delete preserves moderation history while hiding the record from public endpoints.
    [HttpDelete("{id:guid}/mine")]
    [Authorize(Roles = AppRoles.Interviewer)]
    public async Task<IActionResult> DeleteMine(Guid id, CancellationToken ct)
    {
        var requesterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await service.DeleteMineAsync(id, requesterId, ct) ? NoContent() : NotFound();
    }

}
