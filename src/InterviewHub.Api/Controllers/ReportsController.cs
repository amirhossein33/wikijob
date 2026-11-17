using InterviewHub.Business.Auth;
using InterviewHub.Business.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewHub.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(ReportQueryService reports) : ControllerBase
{
    [HttpGet("market")]
    [AllowAnonymous]
    public async Task<ActionResult<MarketReportResponse>> Market([FromQuery] ReportFilterDto filter, CancellationToken ct)
    {
        if (!Valid(filter)) return BadRequest(new { error = "Invalid report filters." });
        return Ok(await reports.MarketAsync(filter, ct));
    }

    [HttpGet("companies/{companyId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CompanyReportResponse>> Company(Guid companyId,
        [FromQuery] ReportFilterDto filter, CancellationToken ct)
    {
        if (!Valid(filter)) return BadRequest(new { error = "Invalid report filters." });
        return Ok(await reports.CompanyAsync(companyId, filter, ct));
    }

    [HttpGet("companies/compare")]
    [AllowAnonymous]
    public async Task<ActionResult<CompanyComparisonResponse>> Compare([FromQuery] Guid left,
        [FromQuery] Guid right, [FromQuery] ReportFilterDto filter, CancellationToken ct)
    {
        if (left == Guid.Empty || right == Guid.Empty || left == right || !Valid(filter))
            return BadRequest(new { error = "Two distinct company IDs and valid filters are required." });
        return Ok(await reports.CompareAsync(left, right, filter, ct));
    }

    [HttpGet("interviewers/priorities")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PriorityReportRow>>> Priorities(CancellationToken ct) =>
        Ok(await reports.PrioritiesAsync(ct));

    private static bool Valid(ReportFilterDto filter) => !(filter.From is not null && filter.To is not null && filter.From > filter.To)
        && (filter.Level is null || Enum.IsDefined(filter.Level.Value))
        && (filter.Type is null || Enum.IsDefined(filter.Type.Value));
}

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/reports")]
public sealed class AdminReportsController(ReportSummaryService summaries) : ControllerBase
{
    // Manual rebuild repairs summaries after out-of-band database changes.
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        await summaries.RebuildAsync(ct);
        return NoContent();
    }
}
