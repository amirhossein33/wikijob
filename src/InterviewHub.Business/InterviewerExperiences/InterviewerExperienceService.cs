using InterviewHub.Data;
using InterviewHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.InterviewerExperiences;

public sealed class InterviewerExperienceService(InterviewHubDbContext db)
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
}
