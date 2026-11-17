using InterviewHub.Data;
using InterviewHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.Reports;

public sealed class ReportQueryService(InterviewHubDbContext db)
{
    private const int MinimumGroupSize = 5;

    public async Task<MarketReportResponse> MarketAsync(ReportFilterDto filter, CancellationToken ct)
    {
        var questions = await QuestionRows(filter).ToListAsync(ct);
        var visibleQuestions = questions.GroupBy(x => x.QuestionId)
            .Where(g => g.Sum(x => x.ExperienceCount) >= MinimumGroupSize).ToArray();
        var topQuestions = visibleQuestions.OrderByDescending(g => g.Sum(x => x.ExperienceCount)).Take(20)
            .Select(g => new TopQuestionRow(g.Key, g.First().Question.Text, g.First().Question.Type,
                g.Sum(x => x.ExperienceCount))).ToArray();
        var types = questions.GroupBy(x => x.Question.Type)
            .Where(g => g.Sum(x => x.ExperienceCount) >= MinimumGroupSize)
            .Select(g => new TypeDistributionRow(g.Key, g.Sum(x => x.ExperienceCount)))
            .OrderByDescending(x => x.QuestionMentions).ToArray();
        var tags = questions.SelectMany(row => row.Question.QuestionTags.Select(link =>
                new { link.TagId, link.Tag.Name, row.ExperienceCount }))
            .GroupBy(x => new { x.TagId, x.Name })
            .Where(g => g.Sum(x => x.ExperienceCount) >= MinimumGroupSize)
            .OrderByDescending(g => g.Sum(x => x.ExperienceCount)).Take(20)
            .Select(g => new TopTagRow(g.Key.TagId, g.Key.Name, g.Sum(x => x.ExperienceCount))).ToArray();

        string metric;
        MonthlyTrendRow[] trend;
        if (filter.Type is not null || filter.TagId is not null)
        {
            metric = "QuestionMentions";
            trend = questions.GroupBy(x => new { x.Day.Year, x.Day.Month })
                .Where(g => g.Sum(x => x.ExperienceCount) >= MinimumGroupSize)
                .Select(g => new MonthlyTrendRow(g.Key.Year, g.Key.Month, g.Sum(x => x.ExperienceCount)))
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ToArray();
        }
        else
        {
            metric = "Experiences";
            var companyRows = await CompanyRows(filter).ToListAsync(ct);
            trend = companyRows.GroupBy(x => new { x.Day.Year, x.Day.Month })
                .Where(g => g.Sum(x => x.ExperienceCount) >= MinimumGroupSize)
                .Select(g => new MonthlyTrendRow(g.Key.Year, g.Key.Month, g.Sum(x => x.ExperienceCount)))
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ToArray();
        }
        var refreshed = await db.ReportRefreshStates.AsNoTracking().Where(x => x.Id == 1)
            .Select(x => (DateTimeOffset?)x.LastRefreshedAt).SingleOrDefaultAsync(ct);
        return new MarketReportResponse(refreshed, topQuestions, types, tags, metric, trend);
    }

    public async Task<CompanyReportResponse> CompanyAsync(Guid companyId, ReportFilterDto filter, CancellationToken ct)
    {
        var scoped = filter with { CompanyId = companyId };
        var rows = await CompanyRows(scoped).ToListAsync(ct);
        var count = rows.Sum(x => x.ExperienceCount);
        if (count < MinimumGroupSize)
            return new CompanyReportResponse(companyId, false, null, null, null, null, null, null, null, [], new Dictionary<int, int>());

        var known = rows.Sum(x => x.PassedCount + x.RejectedCount);
        var conductCount = rows.Sum(x => x.ConductRatingCount);
        var alignmentCount = rows.Sum(x => x.AdAlignmentRatingCount);
        var questions = await QuestionRows(scoped).ToListAsync(ct);
        var top = questions.GroupBy(x => x.QuestionId)
            .Where(g => g.Sum(x => x.ExperienceCount) >= MinimumGroupSize)
            .OrderByDescending(g => g.Sum(x => x.ExperienceCount)).Take(20)
            .Select(g => new TopQuestionRow(g.Key, g.First().Question.Text, g.First().Question.Type,
                g.Sum(x => x.ExperienceCount))).ToArray();
        var difficulty = new Dictionary<int, int>();
        for (var value = 1; value <= 5; value++)
        {
            var number = value switch
            {
                1 => questions.Sum(x => x.Difficulty1Count),
                2 => questions.Sum(x => x.Difficulty2Count),
                3 => questions.Sum(x => x.Difficulty3Count),
                4 => questions.Sum(x => x.Difficulty4Count),
                _ => questions.Sum(x => x.Difficulty5Count)
            };
            if (number >= MinimumGroupSize) difficulty.Add(value, number);
        }
        return new CompanyReportResponse(companyId, true, count,
            known >= MinimumGroupSize ? Math.Round((double)rows.Sum(x => x.PassedCount) / known, 3) : null,
            known >= MinimumGroupSize ? known : null,
            conductCount >= MinimumGroupSize ? Math.Round((double)rows.Sum(x => x.ConductRatingSum) / conductCount, 2) : null,
            conductCount >= MinimumGroupSize ? conductCount : null,
            alignmentCount >= MinimumGroupSize ? Math.Round((double)rows.Sum(x => x.AdAlignmentRatingSum) / alignmentCount, 2) : null,
            alignmentCount >= MinimumGroupSize ? alignmentCount : null, top, difficulty);
    }

    public async Task<CompanyComparisonResponse> CompareAsync(Guid left, Guid right,
        ReportFilterDto filter, CancellationToken ct) =>
        new(await CompanyAsync(left, filter, ct), await CompanyAsync(right, filter, ct));

    // Skills from fewer than five published profiles are omitted.
    public async Task<IReadOnlyList<PriorityReportRow>> PrioritiesAsync(CancellationToken ct) =>
        await db.InterviewerPrioritySummaries.AsNoTracking()
            .Where(x => x.ProfileCount >= MinimumGroupSize)
            .OrderByDescending(x => x.ProfileCount).ThenBy(x => x.Skill)
            .Select(x => new PriorityReportRow(x.Skill, x.ProfileCount,
                Math.Round((double)x.ImportanceSum / x.ProfileCount, 2))).ToListAsync(ct);

    private IQueryable<QuestionDailySummary> QuestionRows(ReportFilterDto filter)
    {
        var query = db.QuestionDailySummaries.AsNoTracking()
            .Include(x => x.Question).ThenInclude(x => x.QuestionTags).ThenInclude(x => x.Tag)
            .AsSplitQuery().AsQueryable();
        if (filter.CompanyId is Guid companyId) query = query.Where(x => x.CompanyId == companyId);
        if (filter.Level is ExperienceLevel level) query = query.Where(x => x.Level == level);
        if (filter.Type is QuestionType type) query = query.Where(x => x.Question.Type == type);
        if (filter.TagId is Guid tagId) query = query.Where(x => x.Question.QuestionTags.Any(t => t.TagId == tagId));
        if (filter.From is DateOnly from) query = query.Where(x => x.Day >= from);
        if (filter.To is DateOnly to) query = query.Where(x => x.Day <= to);
        return query;
    }

    private IQueryable<CompanyDailySummary> CompanyRows(ReportFilterDto filter)
    {
        var query = db.CompanyDailySummaries.AsNoTracking().AsQueryable();
        if (filter.CompanyId is Guid companyId) query = query.Where(x => x.CompanyId == companyId);
        if (filter.Level is ExperienceLevel level) query = query.Where(x => x.Level == level);
        if (filter.From is DateOnly from) query = query.Where(x => x.Day >= from);
        if (filter.To is DateOnly to) query = query.Where(x => x.Day <= to);
        return query;
    }
}
