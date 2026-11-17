using System.Security.Claims;
using InterviewHub.Business.Auth;
using InterviewHub.Business.Moderation;
using InterviewHub.Business.CandidateExperiences;
using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewHub.Api.Controllers;

// Every action in this controller requires the Admin role and writes a moderation log.
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/experiences")]
public sealed class AdminExperiencesController(AdminModerationService service) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingExperienceResponse>>> Pending([FromQuery] int take = 50, CancellationToken ct = default) =>
        Ok(await service.PendingAsync(Math.Clamp(take, 1, 100), ct));

    [HttpGet("{type}/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<ModerationLogResponse>>> History(string type, Guid id, CancellationToken ct)
    {
        if (!TryType(type, out var kind)) return BadRequest(new { error = "Type must be candidate or interviewer." });
        return Ok(await service.HistoryAsync(kind, id, ct));
    }

    [HttpPost("{type}/{id:guid}/publish")]
    public Task<IActionResult> Publish(string type, Guid id, ModerationReasonRequest request, CancellationToken ct) =>
        Change(type, id, request, ModerationAction.Published, ct);

    [HttpPost("{type}/{id:guid}/reject")]
    public Task<IActionResult> Reject(string type, Guid id, ModerationReasonRequest request, CancellationToken ct) =>
        Change(type, id, request, ModerationAction.Rejected, ct);

    [HttpDelete("{type}/{id:guid}")]
    public Task<IActionResult> Delete(string type, Guid id, [FromBody] ModerationReasonRequest request, CancellationToken ct) =>
        Change(type, id, request, ModerationAction.Deleted, ct);

    [HttpPut("interviewer/{id:guid}")]
    public async Task<IActionResult> EditInterviewer(Guid id, AdminInterviewerExperienceUpdateRequest request, CancellationToken ct)
    {
        if (request.Experience is null || !TryValidateModel(request.Experience)) return ValidationProblem(ModelState);
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { error = "Reason is required." });
        var result = await service.EditInterviewerAsync(id, AdminId(), request.Experience, request.Reason, ct);
        return Map(result);
    }

    [HttpPut("candidate/{id:guid}")]
    public async Task<IActionResult> EditCandidate(Guid id, AdminCandidateExperienceUpdateRequest request, CancellationToken ct)
    {
        if (request.Experience is null || !TryValidateModel(request.Experience)) return ValidationProblem(ModelState);
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { error = "Reason is required." });
        try { return Map(await service.EditCandidateAsync(id, AdminId(), request, ct)); }
        catch (QuestionSelectionException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private async Task<IActionResult> Change(string type, Guid id, ModerationReasonRequest request, ModerationAction action, CancellationToken ct)
    {
        if (!TryType(type, out var kind)) return BadRequest(new { error = "Type must be candidate or interviewer." });
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { error = "Reason is required." });
        return Map(await service.ChangeStatusAsync(kind, id, AdminId(), action, request.Reason, ct));
    }

    private Guid AdminId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static bool TryType(string value, out ModeratedContentType type)
    {
        type = value.ToLowerInvariant() switch
        {
            "candidate" => ModeratedContentType.CandidateExperience,
            "interviewer" => ModeratedContentType.InterviewerExperience,
            _ => (ModeratedContentType)(-1)
        };
        return Enum.IsDefined(type);
    }

    private IActionResult Map(ModerationResult result) => result switch
    {
        ModerationResult.Success => NoContent(),
        ModerationResult.NotFound => NotFound(),
        ModerationResult.InvalidQuestion => BadRequest(new { error = "One or more Question IDs do not exist." }),
        _ => StatusCode(500)
    };
}
