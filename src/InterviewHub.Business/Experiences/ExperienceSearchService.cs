using InterviewHub.Data;
using InterviewHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.Experiences;

public sealed record ExperienceSearchFilter(Guid? CompanyId, ExperienceLevel? Level,
    QuestionType? QuestionType, Guid? TagId, DateOnly? From, DateOnly? To,
    string? Kind, int Page = 1, int PageSize = 20);

// List DTO has only public fields; hidden company and author values are projected as null.
public sealed record ExperienceListItem(Guid Id, string Kind, DateOnly InterviewDate,
    ExperienceLevel Level, string? AuthorName, string? CompanyName,
    byte? InterviewRating, byte? InterviewerConductRating, byte? JobAdAlignmentRating);
public sealed record ExperienceSearchResponse(int Page, int PageSize, int TotalCount,
    IReadOnlyList<ExperienceListItem> Items);
public sealed record CompanyListItem(Guid Id, string Name);

public sealed class ExperienceSearchService(InterviewHubDbContext db)
{
    // Fetch bounded slices from each source, then combine them in date order.
    public async Task<ExperienceSearchResponse> SearchAsync(ExperienceSearchFilter filter, CancellationToken ct)
    {
        var maxNeeded = checked(filter.Page * filter.PageSize);
        var candidate = db.InterviewExperiences.AsNoTracking().Where(x => x.Status == ModerationStatus.Published);
        var interviewer = db.InterviewerExperiences.AsNoTracking().Where(x => x.Status == ModerationStatus.Published);
        if (filter.Kind == "candidate") interviewer = interviewer.Where(_ => false);
        if (filter.Kind == "interviewer") candidate = candidate.Where(_ => false);
        if (filter.CompanyId is Guid companyId)
        {
            candidate = candidate.Where(x => x.CompanyId == companyId);
            // Company filtering must not reveal a hidden interviewer-company association.
            interviewer = interviewer.Where(x => x.CompanyId == companyId && x.ShowCompanyName);
        }
        if (filter.Level is ExperienceLevel level)
        {
            candidate = candidate.Where(x => x.Level == level);
            interviewer = interviewer.Where(x => x.Level == level);
        }
        if (filter.From is DateOnly from)
        {
            candidate = candidate.Where(x => x.InterviewDate >= from);
            interviewer = interviewer.Where(x => x.InterviewDate >= from);
        }
        if (filter.To is DateOnly to)
        {
            candidate = candidate.Where(x => x.InterviewDate <= to);
            interviewer = interviewer.Where(x => x.InterviewDate <= to);
        }
        if (filter.QuestionType is QuestionType type)
        {
            candidate = candidate.Where(x => x.Stages.Any(s => s.Questions.Any(q => q.Question.Type == type)));
            interviewer = interviewer.Where(x => x.Questions.Any(q => q.Type == type));
        }
        if (filter.TagId is Guid tagId)
        {
            candidate = candidate.Where(x => x.Stages.Any(s => s.Questions.Any(q => q.Question.QuestionTags.Any(t => t.TagId == tagId))));
            interviewer = interviewer.Where(x => x.Questions.Any(q => q.Question != null && q.Question.QuestionTags.Any(t => t.TagId == tagId)));
        }
        var candidateCount = await candidate.CountAsync(ct);
        var interviewerCount = await interviewer.CountAsync(ct);
        var candidateItems = await candidate.OrderByDescending(x => x.InterviewDate).ThenByDescending(x => x.CreatedAt)
            .Take(maxNeeded).Select(x => new ExperienceListItem(x.Id, "candidate", x.InterviewDate, x.Level,
                x.IsAuthorAnonymous ? null : x.Author.UserName, x.Company != null ? x.Company.Name : null,
                null, x.InterviewerConductRating, x.JobAdAlignmentRating)).ToListAsync(ct);
        var interviewerItems = await interviewer.OrderByDescending(x => x.InterviewDate).ThenByDescending(x => x.CreatedAt)
            .Take(maxNeeded).Select(x => new ExperienceListItem(x.Id, "interviewer", x.InterviewDate, x.Level,
                x.ShowInterviewerName ? x.InterviewerName : null,
                x.ShowCompanyName ? x.Company.Name : null,
                x.InterviewRating, null, null)).ToListAsync(ct);
        var items = candidateItems.Concat(interviewerItems).OrderByDescending(x => x.InterviewDate)
            .ThenBy(x => x.Kind).ThenBy(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return new ExperienceSearchResponse(filter.Page, filter.PageSize, candidateCount + interviewerCount, items);
    }

    // Only companies visible through published content are listed publicly.
    public async Task<IReadOnlyList<CompanyListItem>> CompaniesAsync(CancellationToken ct) =>
        await db.Companies.AsNoTracking()
            .Where(c => c.Experiences.Any(e => e.Status == ModerationStatus.Published)
                || c.InterviewerExperiences.Any(e => e.Status == ModerationStatus.Published && e.ShowCompanyName))
            .OrderBy(c => c.Name).Select(c => new CompanyListItem(c.Id, c.Name)).ToListAsync(ct);
}
