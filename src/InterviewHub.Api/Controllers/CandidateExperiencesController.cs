using System.Security.Claims;
using InterviewHub.Business.Auth;
using InterviewHub.Business.CandidateExperiences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterviewHub.Api.Controllers;

[ApiController]
[Route("api/candidate-experiences")]
public sealed class CandidateExperiencesController(CandidateExperienceService service) : ControllerBase
{
    // Only a Candidate or Admin can submit; the author ID comes from the validated JWT.
    [HttpPost]
    [Authorize(Roles = AppRoles.Candidate + "," + AppRoles.Admin)]
    [EnableRateLimiting("submit")]
    public async Task<ActionResult<PrivateCandidateExperienceResponse>> Create(
        CreateCandidateExperienceRequest request, CancellationToken ct)
    {
        var authorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try { return Ok(await service.CreateAsync(authorId, request, ct)); }
        catch (QuestionSelectionException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Published content uses a DTO that omits anonymous identity.
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicCandidateExperienceResponse>> GetPublic(Guid id, CancellationToken ct)
    {
        var item = await service.GetPublicAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    // Owner and Admin can inspect the complete record while it awaits review.
    [HttpGet("{id:guid}/mine")]
    [Authorize]
    public async Task<ActionResult<PrivateCandidateExperienceResponse>> GetMine(Guid id, CancellationToken ct)
    {
        var requesterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var item = await service.GetPrivateAsync(id, requesterId, User.IsInRole(AppRoles.Admin), ct);
        return item is null ? NotFound() : Ok(item);
    }

    // The dashboard requests only the authenticated author's private submissions.
    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Candidate + "," + AppRoles.Admin)]
    public async Task<ActionResult<IReadOnlyList<PrivateCandidateExperienceResponse>>> Mine(CancellationToken ct)
    {
        var authorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await service.MineAsync(authorId, ct));
    }

    // Updating a submission resets it to Pending so edited public content is reviewed again.
    [HttpPut("{id:guid}/mine")]
    [Authorize(Roles = AppRoles.Candidate)]
    public async Task<ActionResult<PrivateCandidateExperienceResponse>> UpdateMine(
        Guid id, CreateCandidateExperienceRequest request, CancellationToken ct)
    {
        var requesterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            var item = await service.UpdateAsync(id, requesterId, false, request, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (QuestionSelectionException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Soft delete removes the submission from public feeds and aggregates.
    [HttpDelete("{id:guid}/mine")]
    [Authorize(Roles = AppRoles.Candidate)]
    public async Task<IActionResult> DeleteMine(Guid id, CancellationToken ct)
    {
        var requesterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await service.DeleteMineAsync(id, requesterId, ct) ? NoContent() : NotFound();
    }
}
