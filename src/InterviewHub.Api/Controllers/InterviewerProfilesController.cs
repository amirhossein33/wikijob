using System.Security.Claims;
using InterviewHub.Business.Auth;
using InterviewHub.Business.InterviewerProfiles;
using InterviewHub.Business.Moderation;
using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewHub.Api.Controllers;

[ApiController]
[Route("api/interviewer-profiles")]
public sealed class InterviewerProfilesController(InterviewerProfileService profiles) : ControllerBase
{
    // The authenticated owner submits all profile fields, including hidden ones.
    [HttpPut("me")]
    [Authorize(Roles = AppRoles.Interviewer + "," + AppRoles.Admin)]
    public async Task<ActionResult<PrivateInterviewerProfileResponse>> Save(
        SaveInterviewerProfileRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (result, profile) = await profiles.SaveAsync(userId, request, ct);
        return result == ProfileSaveResult.Deleted
            ? Conflict(new { error = "Deleted profiles cannot be edited." }) : Ok(profile);
    }

    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Interviewer + "," + AppRoles.Admin)]
    public async Task<ActionResult<PrivateInterviewerProfileResponse>> Mine(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await profiles.GetPrivateAsync(userId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    // The opaque public ID avoids exposing the Identity user ID in public profile URLs.
    [HttpGet("{publicId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicInterviewerProfileResponse>> Public(Guid publicId, CancellationToken ct)
    {
        var profile = await profiles.GetPublicAsync(publicId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }
}

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/interviewer-profiles")]
public sealed class AdminInterviewerProfilesController(InterviewerProfileService profiles,
    AdminModerationService moderation) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingInterviewerProfileResponse>>> Pending(
        [FromQuery] int take = 50, CancellationToken ct = default) =>
        Ok(await profiles.PendingAsync(Math.Clamp(take, 1, 100), ct));

    // Admin preview includes hidden fields so privacy can be checked before publication.
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<PrivateInterviewerProfileResponse>> Preview(Guid userId, CancellationToken ct)
    {
        var profile = await profiles.GetPrivateAsync(userId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("{userId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<ModerationLogResponse>>> History(Guid userId, CancellationToken ct) =>
        Ok(await moderation.HistoryAsync(ModeratedContentType.InterviewerProfile, userId, ct));

    [HttpPost("{userId:guid}/publish")]
    public Task<IActionResult> Publish(Guid userId, ModerationReasonRequest request, CancellationToken ct) =>
        Change(userId, request, ModerationAction.Published, ct);

    [HttpPost("{userId:guid}/reject")]
    public Task<IActionResult> Reject(Guid userId, ModerationReasonRequest request, CancellationToken ct) =>
        Change(userId, request, ModerationAction.Rejected, ct);

    [HttpDelete("{userId:guid}")]
    public Task<IActionResult> Delete(Guid userId, [FromBody] ModerationReasonRequest request, CancellationToken ct) =>
        Change(userId, request, ModerationAction.Deleted, ct);

    private async Task<IActionResult> Change(Guid userId, ModerationReasonRequest request,
        ModerationAction action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { error = "Reason is required." });
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await profiles.ModerateAsync(userId, adminId, action, request.Reason, ct) ? NoContent() : NotFound();
    }
}
