using InterviewHub.Data;
using InterviewHub.Data.Entities;
using InterviewHub.Business.Reports;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.CandidateExperiences;

public sealed class CandidateExperienceService(InterviewHubDbContext db, ReportSummaryService reports)
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

    // The authenticated author's list includes private moderation states but never other users' content.
    public async Task<IReadOnlyList<PrivateCandidateExperienceResponse>> MineAsync(Guid authorId, CancellationToken ct)
    {
        var items = await Query().Where(x => x.AuthorId == authorId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return items.Select(ToPrivate).ToArray();
    }

    // Owner edits replace all submitted fields and return the record to moderation.
    public async Task<PrivateCandidateExperienceResponse?> UpdateAsync(Guid id, Guid requesterId,
        bool isAdmin, CreateCandidateExperienceRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.InterviewExperiences
            .Include(x => x.Author).Include(x => x.Company)
            .SingleOrDefaultAsync(x => x.Id == id && (isAdmin || x.AuthorId == requesterId), ct);
        if (item is null || item.Status == ModerationStatus.Deleted) return null;

        var previousStatus = item.Status;
        var stageIds = db.InterviewStages.Where(stage => stage.InterviewExperienceId == id).Select(stage => stage.Id);
        await db.InterviewStageQuestions.Where(question => stageIds.Contains(question.InterviewStageId)).ExecuteDeleteAsync(ct);
        await db.InterviewStages.Where(stage => stage.InterviewExperienceId == id).ExecuteDeleteAsync(ct);
        // Bulk deletion bypasses EF tracking; reload the root before attaching the replacement graph.
        db.ChangeTracker.Clear();
        item = await db.InterviewExperiences.Include(x => x.Author).Include(x => x.Company)
            .SingleAsync(x => x.Id == id, ct);
        item.Company = string.IsNullOrWhiteSpace(request.CompanyName)
            ? null : await GetOrCreateCompanyAsync(request.CompanyName.Trim(), ct);
        item.IsAuthorAnonymous = request.IsAuthorAnonymous;
        item.InterviewDate = request.Stages.Min(s => s.Date);
        item.Level = request.Level;
        item.Outcome = request.Outcome;
        item.Description = request.Description?.Trim();
        item.JobAdText = request.JobAdText?.Trim();
        item.JobAdUrl = request.JobAdUrl?.Trim();
        item.InterviewerConductRating = request.InterviewerConductRating;
        item.JobAdAlignmentRating = request.JobAdAlignmentRating;
        item.RatingReason = request.RatingReason?.Trim();
        item.Status = ModerationStatus.Pending;

        // Replacing stages atomically avoids retaining removed question occurrences.
        var canonicalQuestions = new Dictionary<(string Text, QuestionType Type), Question>();
        for (var stageIndex = 0; stageIndex < request.Stages.Count; stageIndex++)
        {
            var input = request.Stages[stageIndex];
            var stage = new InterviewStage
            {
                Id = Guid.NewGuid(), Order = stageIndex + 1, Type = input.Type,
                InterviewExperienceId = item.Id, Date = input.Date, Description = input.Description.Trim()
            };
            for (var questionIndex = 0; questionIndex < input.Questions.Count; questionIndex++)
            {
                var questionInput = input.Questions[questionIndex];
                Question canonical;
                if (questionInput.QuestionId is Guid selectedId)
                {
                    canonical = await db.Questions.SingleOrDefaultAsync(x => x.Id == selectedId, ct)
                        ?? throw new QuestionSelectionException("Selected QuestionId does not exist.");
                    if (canonical.MergedIntoQuestionId is Guid targetId)
                        canonical = await db.Questions.SingleAsync(x => x.Id == targetId, ct);
                }
                else
                {
                    var key = (questionInput.Text!.Trim(), questionInput.Type);
                    if (!canonicalQuestions.TryGetValue(key, out canonical!))
                    {
                        canonical = await db.Questions.Where(x => x.Text == key.Item1 && x.Type == key.Type)
                            .OrderBy(x => x.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct)
                            ?? new Question { Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type };
                        if (canonical.MergedIntoQuestionId is Guid targetId)
                            canonical = await db.Questions.SingleAsync(x => x.Id == targetId, ct);
                        canonicalQuestions.Add(key, canonical);
                    }
                }
                stage.Questions.Add(new InterviewStageQuestion
                {
                    Id = Guid.NewGuid(), Order = questionIndex + 1,
                    Question = canonical, PerceivedDifficulty = questionInput.PerceivedDifficulty
                });
            }
            // Explicitly mark a client-generated key as new so EF inserts rather than updates it.
            db.InterviewStages.Add(stage);
        }

        await db.SaveChangesAsync(ct);
        if (previousStatus == ModerationStatus.Published)
            await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetPrivateAsync(id, requesterId, isAdmin, ct);
    }

    // Deletion is soft so submitted content remains available to moderation and audit flows.
    public async Task<bool> DeleteMineAsync(Guid id, Guid requesterId, CancellationToken ct)
    {
        var item = await db.InterviewExperiences.SingleOrDefaultAsync(x => x.Id == id && x.AuthorId == requesterId, ct);
        if (item is null || item.Status == ModerationStatus.Deleted) return false;
        var wasPublished = item.Status == ModerationStatus.Published;
        item.Status = ModerationStatus.Deleted;
        await db.SaveChangesAsync(ct);
        if (wasPublished) await reports.RebuildAsync(ct);
        return true;
    }

    private IQueryable<InterviewExperience> Query() => db.InterviewExperiences.AsNoTracking()
        .Include(x => x.Author).Include(x => x.Company)
        .Include(x => x.Stages).ThenInclude(x => x.Questions).ThenInclude(x => x.Question)
        .AsSplitQuery();

    private async Task<Company> GetOrCreateCompanyAsync(string name, CancellationToken ct)
    {
        var existing = await db.Companies.SingleOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null) return existing;
        var company = new Company { Id = Guid.NewGuid(), Name = name };
        db.Companies.Add(company);
        return company;
    }

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
