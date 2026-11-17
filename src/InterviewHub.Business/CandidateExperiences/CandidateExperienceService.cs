using InterviewHub.Data;
using InterviewHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.CandidateExperiences;

public sealed class CandidateExperienceService(InterviewHubDbContext db)
{
    // Canonical questions are reused by exact text and type until manual merge is available.
    public async Task<PrivateCandidateExperienceResponse> CreateAsync(Guid authorId,
        CreateCandidateExperienceRequest request, CancellationToken ct)
    {
        Company? company = null;
        if (!string.IsNullOrWhiteSpace(request.CompanyName))
        {
            var name = request.CompanyName.Trim();
            company = await db.Companies.SingleOrDefaultAsync(x => x.Name == name, ct)
                ?? new Company { Id = Guid.NewGuid(), Name = name };
            if (db.Entry(company).State == EntityState.Detached) db.Companies.Add(company);
        }

        var author = await db.Users.SingleAsync(x => x.Id == authorId, ct);
        var item = new InterviewExperience
        {
            Id = Guid.NewGuid(), AuthorId = authorId, Author = author,
            Company = company, IsAuthorAnonymous = request.IsAuthorAnonymous,
            InterviewDate = request.Stages.Min(s => s.Date), CreatedAt = DateTimeOffset.UtcNow,
            Level = request.Level, Outcome = request.Outcome,
            Description = request.Description?.Trim(), JobAdText = request.JobAdText?.Trim(),
            JobAdUrl = request.JobAdUrl?.Trim(), InterviewerConductRating = request.InterviewerConductRating,
            JobAdAlignmentRating = request.JobAdAlignmentRating, RatingReason = request.RatingReason?.Trim()
        };

        var questions = new Dictionary<(string Text, QuestionType Type), Question>();
        for (var stageIndex = 0; stageIndex < request.Stages.Count; stageIndex++)
        {
            var input = request.Stages[stageIndex];
            var stage = new InterviewStage
            {
                Id = Guid.NewGuid(), Order = stageIndex + 1, Type = input.Type,
                Date = input.Date, Description = input.Description.Trim()
            };
            for (var questionIndex = 0; questionIndex < input.Questions.Count; questionIndex++)
            {
                var q = input.Questions[questionIndex];
                Question canonical;
                if (q.QuestionId is Guid selectedId)
                {
                    canonical = await db.Questions.SingleOrDefaultAsync(x => x.Id == selectedId, ct)
                        ?? throw new QuestionSelectionException("Selected QuestionId does not exist.");
                    if (canonical.MergedIntoQuestionId is Guid targetId)
                        canonical = await db.Questions.SingleAsync(x => x.Id == targetId, ct);
                }
                else
                {
                    var key = (q.Text!.Trim(), q.Type);
                    if (!questions.TryGetValue(key, out canonical!))
                    {
                        canonical = await db.Questions.Where(x => x.Text == key.Item1 && x.Type == key.Type)
                            .OrderBy(x => x.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct)
                            ?? new Question { Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type };
                        if (canonical.MergedIntoQuestionId is Guid targetId)
                            canonical = await db.Questions.SingleAsync(x => x.Id == targetId, ct);
                        questions.Add(key, canonical);
                    }
                }
                stage.Questions.Add(new InterviewStageQuestion
                {
                    Id = Guid.NewGuid(), Order = questionIndex + 1,
                    Question = canonical, PerceivedDifficulty = q.PerceivedDifficulty
                });
            }
            item.Stages.Add(stage);
        }

        db.InterviewExperiences.Add(item);
        await db.SaveChangesAsync(ct);
        return ToPrivate(item);
    }

    // The public query filters before materializing and never projects an anonymous author ID.
    public async Task<PublicCandidateExperienceResponse?> GetPublicAsync(Guid id, CancellationToken ct)
    {
        var item = await Query().SingleOrDefaultAsync(x => x.Id == id && x.Status == ModerationStatus.Published, ct);
        return item is null ? null : new PublicCandidateExperienceResponse(item.Id,
            item.IsAuthorAnonymous ? null : item.Author.UserName, item.Company?.Name,
            item.InterviewDate, item.Level, item.Outcome, item.Description, item.JobAdText,
            item.JobAdUrl, item.InterviewerConductRating, item.JobAdAlignmentRating,
            item.RatingReason, ToStages(item));
    }

    public async Task<PrivateCandidateExperienceResponse?> GetPrivateAsync(Guid id, Guid requesterId,
        bool isAdmin, CancellationToken ct)
    {
        var item = await Query().SingleOrDefaultAsync(x => x.Id == id && (isAdmin || x.AuthorId == requesterId), ct);
        return item is null ? null : ToPrivate(item);
    }

    private IQueryable<InterviewExperience> Query() => db.InterviewExperiences.AsNoTracking()
        .Include(x => x.Author).Include(x => x.Company)
        .Include(x => x.Stages).ThenInclude(x => x.Questions).ThenInclude(x => x.Question)
        .AsSplitQuery();

    private static PrivateCandidateExperienceResponse ToPrivate(InterviewExperience item) =>
        new(item.Id, item.AuthorId, item.Author.UserName!, item.IsAuthorAnonymous, item.Company?.Name,
            item.InterviewDate, item.Level, item.Outcome, item.Description, item.JobAdText,
            item.JobAdUrl, item.InterviewerConductRating, item.JobAdAlignmentRating,
            item.RatingReason, item.Status, ToStages(item));

    private static IReadOnlyList<CandidateStageResponse> ToStages(InterviewExperience item) =>
        item.Stages.OrderBy(x => x.Order).Select(s => new CandidateStageResponse(s.Id, s.Order, s.Type,
            s.Date, s.Description, s.Questions.OrderBy(q => q.Order).Select(q =>
                new CandidateQuestionResponse(q.QuestionId, q.Question.Text, q.Question.Type, q.PerceivedDifficulty)).ToArray())).ToArray();
}
