using InterviewHub.Business.Experiences;
using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewHub.Api.Controllers;

[ApiController]
[Route("api/experiences")]
public sealed class ExperienceSearchController(ExperienceSearchService search) : ControllerBase
{
    // All filters apply only to published records and use privacy-safe projections.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ExperienceSearchResponse>> List([FromQuery] ExperienceSearchFilter filter,
        CancellationToken ct)
    {
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 ||
            (long)filter.Page * filter.PageSize > 100_000 ||
            filter.From > filter.To ||
            filter.Level is not null && !Enum.IsDefined(filter.Level.Value) ||
            filter.QuestionType is not null && !Enum.IsDefined(filter.QuestionType.Value) ||
            filter.Kind is not null && filter.Kind is not ("candidate" or "interviewer"))
            return BadRequest(new { error = "Invalid experience filters." });
        return Ok(await search.SearchAsync(filter, ct));
    }
}

[ApiController]
[Route("api/companies")]
public sealed class CompaniesController(ExperienceSearchService search) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CompanyListItem>>> List(CancellationToken ct) =>
        Ok(await search.CompaniesAsync(ct));
}
