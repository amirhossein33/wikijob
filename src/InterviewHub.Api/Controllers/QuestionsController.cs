using InterviewHub.Business.Auth;
using InterviewHub.Business.Questions;
using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewHub.Api.Controllers;

[ApiController]
[Route("api/questions")]
public sealed class QuestionsController(QuestionCatalogService catalog) : ControllerBase
{
    // Search stays public but excludes pending-only and merged source questions.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<QuestionSearchResponse>> Search([FromQuery] string? q,
        [FromQuery] QuestionType? type, [FromQuery] Guid? tagId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || type is not null && !Enum.IsDefined(type.Value))
            return BadRequest(new { error = "Invalid search filters." });
        return Ok(await catalog.SearchAsync(q, type, tagId, page, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<QuestionResponse>> Get(Guid id, CancellationToken ct)
    {
        var question = await catalog.GetAsync(id, ct);
        return question is null ? NotFound() : Ok(question);
    }

    // Members can suggest questions; they appear in public search after a linked experience is approved.
    [HttpPost]
    [Authorize(Roles = AppRoles.Candidate + "," + AppRoles.Interviewer + "," + AppRoles.Admin)]
    public async Task<ActionResult<QuestionCreatedResponse>> Create(CreateQuestionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Type is null || !Enum.IsDefined(request.Type.Value))
            return BadRequest(new { error = "Valid question text and type are required." });
        return Ok(await catalog.CreateQuestionAsync(request, ct));
    }

    [HttpPut("{id:guid}/tags")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetTags(Guid id, SetQuestionTagsRequest request, CancellationToken ct)
    {
        if (request.TagIds is null) return BadRequest(new { error = "TagIds are required." });
        return await catalog.SetTagsAsync(id, request.TagIds, ct) switch
        {
            QuestionCatalogResult.Success => NoContent(),
            QuestionCatalogResult.NotFound => NotFound(),
            QuestionCatalogResult.InvalidTag => BadRequest(new { error = "Unknown TagId." }),
            _ => StatusCode(500)
        };
    }
}

[ApiController]
[Route("api/tags")]
public sealed class TagsController(QuestionCatalogService catalog) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> List(CancellationToken ct) =>
        Ok(await catalog.TagsAsync(ct));

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<TagResponse>> Create(CreateTagRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });
        return Ok(await catalog.CreateTagAsync(request, ct));
    }
}

[ApiController]
[Route("api/admin/questions")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminQuestionsController(QuestionCatalogService catalog) : ControllerBase
{
    // Manual merge is restricted to same-type canonical questions.
    [HttpPost("merge")]
    public async Task<IActionResult> Merge(MergeQuestionRequest request, CancellationToken ct) =>
        await catalog.MergeAsync(request, ct) switch
        {
            QuestionCatalogResult.Success => NoContent(),
            QuestionCatalogResult.NotFound => NotFound(),
            QuestionCatalogResult.SameQuestion or QuestionCatalogResult.TypeMismatch or QuestionCatalogResult.AlreadyMerged
                => BadRequest(new { error = "Questions must be distinct, canonical, and of the same type." }),
            _ => StatusCode(500)
        };
}
