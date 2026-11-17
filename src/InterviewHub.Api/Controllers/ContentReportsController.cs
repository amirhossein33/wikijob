using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using InterviewHub.Business.Auth;
using InterviewHub.Data;
using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace InterviewHub.Api.Controllers;

// Authenticated readers can flag a published candidate experience once for moderator review.
[ApiController]
[Route("api/content-reports")]
public sealed class ContentReportsController(InterviewHubDbContext db) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [EnableRateLimiting("submit")]
    public async Task<IActionResult> Create(CreateContentReportRequest request, CancellationToken ct)
    {
        var reporterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await db.InterviewExperiences.AnyAsync(x => x.Id == request.ExperienceId && x.Status == ModerationStatus.Published, ct))
            return NotFound();
        if (await db.ContentReports.AnyAsync(x => x.InterviewExperienceId == request.ExperienceId && x.ReporterId == reporterId, ct))
            return Conflict(new { error = "This account has already reported this experience." });

        var report = new ContentReport
        {
            Id = Guid.NewGuid(), InterviewExperienceId = request.ExperienceId, ReporterId = reporterId,
            Reason = request.Reason.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.ContentReports.Add(report);
        await db.SaveChangesAsync(ct);
        return Ok(new { report.Id, report.Status });
    }
}

// Reports are private moderation data and never include the reporter in public projections.
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/content-reports")]
public sealed class AdminContentReportsController(InterviewHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportStatus status = ReportStatus.Open, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(status)) return BadRequest();
        var reports = await db.ContentReports.AsNoTracking().Where(x => x.Status == status)
            .OrderBy(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, x.InterviewExperienceId, x.ReporterId, x.Reason, x.CreatedAt, x.Status })
            .ToListAsync(ct);
        return Ok(reports);
    }

    [HttpPost("{id:guid}/resolve")]
    public Task<IActionResult> Resolve(Guid id, CancellationToken ct) => Change(id, ReportStatus.Resolved, ct);

    [HttpPost("{id:guid}/dismiss")]
    public Task<IActionResult> Dismiss(Guid id, CancellationToken ct) => Change(id, ReportStatus.Dismissed, ct);

    private async Task<IActionResult> Change(Guid id, ReportStatus status, CancellationToken ct)
    {
        var report = await db.ContentReports.FindAsync([id], ct);
        if (report is null) return NotFound();
        if (report.Status != ReportStatus.Open) return Conflict(new { error = "Report was already reviewed." });
        report.Status = status;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record CreateContentReportRequest(
    [Required] Guid ExperienceId,
    [Required, StringLength(2000, MinimumLength = 10)] string Reason);
