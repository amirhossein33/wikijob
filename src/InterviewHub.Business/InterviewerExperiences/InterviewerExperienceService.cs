using InterviewHub.Data;
using InterviewHub.Data.Entities;
using InterviewHub.Business.Reports;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.InterviewerExperiences;

public sealed class InterviewerExperienceService(InterviewHubDbContext db, ReportSummaryService reports)
{
    // Only authenticated interviewer users reach this method through the API policy.
    public async Task<PrivateInterviewerExperienceResponse> CreateAsync(Guid authorId, CreateInterviewerExperienceRequest request, CancellationToken ct)
    {
        var companyName = request.CompanyName.Trim();
        var company = await db.Companies.SingleOrDefaultAsync(x => x.Name == companyName, ct);
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = companyName };
            db.Companies.Add(company);
        }

        var experience = new InterviewerExperience
        {
            Id = Guid.NewGuid(), AuthorId = authorId,
            InterviewerName = request.InterviewerName.Trim(),
            ShowInterviewerName = request.ShowInterviewerName,
            Company = company, ShowCompanyName = request.ShowCompanyName,
            InterviewDate = request.InterviewDate, Level = request.Level,
            Description = request.Description.Trim(),
            JobAdText = request.JobAdText?.Trim(), JobAdUrl = request.JobAdUrl?.Trim(),
            InterviewRating = request.InterviewRating, CreatedAt = DateTimeOffset.UtcNow
        };
        var canonicalQuestions = new Dictionary<(string Text, QuestionType Type), Question>();
        foreach (var input in request.Questions)
        {
            var key = (input.Text.Trim(), input.Type);
            if (!canonicalQuestions.TryGetValue(key, out var canonical))
            {
                canonical = await db.Questions.Where(q => q.Text == key.Item1 && q.Type == key.Type)
                    .OrderBy(q => q.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct)
                    ?? new Question { Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type };
                if (canonical.MergedIntoQuestionId is Guid targetId)
                    canonical = await db.Questions.SingleAsync(q => q.Id == targetId, ct);
                canonicalQuestions.Add(key, canonical);
            }
            experience.Questions.Add(new InterviewerExperienceQuestion
            {
                Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type, Question = canonical
            });
        }
        db.InterviewerExperiences.Add(experience);
        await db.SaveChangesAsync(ct);
        return ToPrivate(experience);
    }

    // A pending or hidden submission is never returned from the public endpoint.
    public async Task<PublicInterviewerExperienceResponse?> GetPublicAsync(Guid id, CancellationToken ct)
    {
        var item = await Query().SingleOrDefaultAsync(x => x.Id == id && x.Status == ModerationStatus.Published, ct);
        return item is null ? null : ToPublic(item);
    }

    public async Task<PrivateInterviewerExperienceResponse?> GetPrivateAsync(Guid id, Guid requesterId, bool isAdmin, CancellationToken ct)
    {
        var item = await Query().SingleOrDefaultAsync(x => x.Id == id && (isAdmin || x.AuthorId == requesterId), ct);
        return item is null ? null : ToPrivate(item);
    }

    // The owner dashboard lists all statuses without leaking another account's submissions.
    public async Task<IReadOnlyList<PrivateInterviewerExperienceResponse>> MineAsync(Guid authorId, CancellationToken ct)
    {
        var items = await Query().Where(x => x.AuthorId == authorId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return items.Select(ToPrivate).ToArray();
    }

    // Owner edits require a new moderation decision before the updated version is public.
    public async Task<PrivateInterviewerExperienceResponse?> UpdateAsync(Guid id, Guid requesterId,
        bool isAdmin, CreateInterviewerExperienceRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.InterviewerExperiences.Include(x => x.Company)
            .SingleOrDefaultAsync(x => x.Id == id && (isAdmin || x.AuthorId == requesterId), ct);
        if (item is null || item.Status == ModerationStatus.Deleted) return null;

        var previousStatus = item.Status;
        await db.InterviewerExperienceQuestions.Where(x => x.InterviewerExperienceId == id).ExecuteDeleteAsync(ct);
        // Bulk deletion bypasses EF tracking; reload the root before attaching the replacement graph.
        db.ChangeTracker.Clear();
        item = await db.InterviewerExperiences.Include(x => x.Company).SingleAsync(x => x.Id == id, ct);
        var companyName = request.CompanyName.Trim();
        item.Company = await GetOrCreateCompanyAsync(companyName, ct);
        item.InterviewerName = request.InterviewerName.Trim();
        item.ShowInterviewerName = request.ShowInterviewerName;
        item.ShowCompanyName = request.ShowCompanyName;
        item.InterviewDate = request.InterviewDate;
        item.Level = request.Level;
        item.Description = request.Description.Trim();
        item.JobAdText = request.JobAdText?.Trim();
        item.JobAdUrl = request.JobAdUrl?.Trim();
        item.InterviewRating = request.InterviewRating;
        item.Status = ModerationStatus.Pending;

        var canonicalQuestions = new Dictionary<(string Text, QuestionType Type), Question>();
        foreach (var input in request.Questions)
        {
            var key = (input.Text.Trim(), input.Type);
            if (!canonicalQuestions.TryGetValue(key, out var canonical))
            {
                canonical = await db.Questions.Where(x => x.Text == key.Item1 && x.Type == key.Type)
                    .OrderBy(x => x.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct)
                    ?? new Question { Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type };
                if (canonical.MergedIntoQuestionId is Guid targetId)
                    canonical = await db.Questions.SingleAsync(x => x.Id == targetId, ct);
                canonicalQuestions.Add(key, canonical);
            }
            // Explicitly mark the client-generated key as new instead of treating it as an update.
            db.InterviewerExperienceQuestions.Add(new InterviewerExperienceQuestion
            {
                Id = Guid.NewGuid(), InterviewerExperienceId = id,
                Text = key.Item1, Type = key.Type, Question = canonical
            });
        }

        await db.SaveChangesAsync(ct);
        if (previousStatus == ModerationStatus.Published)
            await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetPrivateAsync(id, requesterId, isAdmin, ct);
    }

    // Soft deletion preserves the review state and keeps public queries from returning the record.
    public async Task<bool> DeleteMineAsync(Guid id, Guid requesterId, CancellationToken ct)
    {
        var item = await db.InterviewerExperiences.SingleOrDefaultAsync(x => x.Id == id && x.AuthorId == requesterId, ct);
        if (item is null || item.Status == ModerationStatus.Deleted) return false;
        var wasPublished = item.Status == ModerationStatus.Published;
        item.Status = ModerationStatus.Deleted;
        await db.SaveChangesAsync(ct);
        if (wasPublished) await reports.RebuildAsync(ct);
        return true;
    }

    private IQueryable<InterviewerExperience> Query() => db.InterviewerExperiences.AsNoTracking()
        .Include(x => x.Company).Include(x => x.Questions);

    private static PublicInterviewerExperienceResponse ToPublic(InterviewerExperience item) =>
        new(item.Id, item.ShowInterviewerName ? item.InterviewerName : null,
            item.ShowCompanyName ? item.Company.Name : null, item.InterviewDate, item.Level,
            item.Description, item.ShowCompanyName ? item.JobAdText : null,
            item.ShowCompanyName ? item.JobAdUrl : null, item.InterviewRating,
            item.Questions.Select(q => new InterviewerQuestionResponse(q.Text, q.Type)).ToArray());

    private static PrivateInterviewerExperienceResponse ToPrivate(InterviewerExperience item) =>
        new(item.Id, item.InterviewerName, item.Company.Name, item.ShowInterviewerName,
            item.ShowCompanyName, item.InterviewDate, item.Level, item.Description,
            item.JobAdText, item.JobAdUrl, item.InterviewRating, item.Status,
            item.Questions.Select(q => new InterviewerQuestionResponse(q.Text, q.Type)).ToArray());

    private async Task<Company> GetOrCreateCompanyAsync(string name, CancellationToken ct)
    {
        var existing = await db.Companies.SingleOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null) return existing;
        var company = new Company { Id = Guid.NewGuid(), Name = name };
        db.Companies.Add(company);
        return company;
    }
}
