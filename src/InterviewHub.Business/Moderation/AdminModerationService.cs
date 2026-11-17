using InterviewHub.Business.InterviewerExperiences;
using InterviewHub.Business.CandidateExperiences;
using InterviewHub.Business.Reports;
using InterviewHub.Data;
using InterviewHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.Moderation;

public enum ModerationResult { Success, NotFound, InvalidQuestion }

public sealed class AdminModerationService(InterviewHubDbContext db, ReportSummaryService reports)
{
    // Pending entries from both author types share one review queue.
    public async Task<IReadOnlyList<PendingExperienceResponse>> PendingAsync(int take, CancellationToken ct)
    {
        var candidate = await db.InterviewExperiences.AsNoTracking()
            .Where(x => x.Status == ModerationStatus.Pending)
            .OrderBy(x => x.CreatedAt).Take(take)
            .Select(x => new PendingExperienceResponse(x.Id, ModeratedContentType.CandidateExperience, x.InterviewDate, x.CreatedAt)).ToListAsync(ct);
        var interviewer = await db.InterviewerExperiences.AsNoTracking()
            .Where(x => x.Status == ModerationStatus.Pending)
            .OrderBy(x => x.CreatedAt).Take(take)
            .Select(x => new PendingExperienceResponse(x.Id, ModeratedContentType.InterviewerExperience, x.InterviewDate, x.CreatedAt)).ToListAsync(ct);
        return candidate.Concat(interviewer).OrderBy(x => x.CreatedAt).Take(take).ToArray();
    }

    public async Task<IReadOnlyList<ModerationLogResponse>> HistoryAsync(ModeratedContentType type, Guid id, CancellationToken ct) =>
        await db.ModerationLogs.AsNoTracking().Where(x => x.ContentType == type && x.ContentId == id)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ModerationLogResponse(x.Id, x.Action, x.PreviousStatus, x.NewStatus, x.Reason, x.CreatedAt, x.AdminId))
            .ToListAsync(ct);

    // A deleted record is retained for audit and cannot be republished.
    public async Task<ModerationResult> ChangeStatusAsync(ModeratedContentType type, Guid id, Guid adminId,
        ModerationAction action, string reason, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (type == ModeratedContentType.CandidateExperience)
        {
            var item = await db.InterviewExperiences.FindAsync([id], ct);
            if (item is null || item.Status == ModerationStatus.Deleted) return ModerationResult.NotFound;
            var previous = item.Status;
            item.Status = action switch
            {
                ModerationAction.Published => ModerationStatus.Published,
                ModerationAction.Rejected => ModerationStatus.Rejected,
                ModerationAction.Deleted => ModerationStatus.Deleted,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
            AddLog(type, id, adminId, action, previous, item.Status, reason);
        }
        else
        {
            var item = await db.InterviewerExperiences.FindAsync([id], ct);
            if (item is null || item.Status == ModerationStatus.Deleted) return ModerationResult.NotFound;
            var previous = item.Status;
            item.Status = action switch
            {
                ModerationAction.Published => ModerationStatus.Published,
                ModerationAction.Rejected => ModerationStatus.Rejected,
                ModerationAction.Deleted => ModerationStatus.Deleted,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
            AddLog(type, id, adminId, action, previous, item.Status, reason);
        }
        await db.SaveChangesAsync(ct);
        await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return ModerationResult.Success;
    }

    // Full replacement avoids partially edited published content.
    public async Task<ModerationResult> EditInterviewerAsync(Guid id, Guid adminId,
        CreateInterviewerExperienceRequest update, string reason, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.InterviewerExperiences.Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || item.Status == ModerationStatus.Deleted) return ModerationResult.NotFound;
        var companyName = update.CompanyName.Trim();
        var company = await GetOrCreateCompanyAsync(companyName, ct);
        item.Company = company;
        item.InterviewerName = update.InterviewerName.Trim();
        item.ShowInterviewerName = update.ShowInterviewerName;
        item.ShowCompanyName = update.ShowCompanyName;
        item.InterviewDate = update.InterviewDate;
        item.Level = update.Level;
        item.Description = update.Description.Trim();
        item.JobAdText = update.JobAdText?.Trim();
        item.JobAdUrl = update.JobAdUrl?.Trim();
        item.InterviewRating = update.InterviewRating;
        // Update tracked rows in place so EF does not issue conflicting child deletes.
        var existingQuestions = item.Questions.OrderBy(q => q.Id).ToList();
        var canonicalQuestions = new Dictionary<(string Text, QuestionType Type), Question>();
        for (var index = 0; index < update.Questions.Count; index++)
        {
            var question = update.Questions[index];
            var key = (question.Text.Trim(), question.Type);
            if (!canonicalQuestions.TryGetValue(key, out var canonical))
            {
                canonical = await db.Questions.Where(q => q.Text == key.Item1 && q.Type == key.Type)
                    .OrderBy(q => q.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct);
                if (canonical is null)
                {
                    canonical = new Question { Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type };
                    db.Questions.Add(canonical);
                }
                if (canonical.MergedIntoQuestionId is Guid targetId)
                    canonical = await db.Questions.SingleAsync(q => q.Id == targetId, ct);
                canonicalQuestions.Add(key, canonical);
            }
            if (index < existingQuestions.Count)
            {
                existingQuestions[index].Text = key.Item1;
                existingQuestions[index].Type = key.Type;
                existingQuestions[index].Question = canonical;
            }
            else
                db.InterviewerExperienceQuestions.Add(new InterviewerExperienceQuestion
                    { Id = Guid.NewGuid(), InterviewerExperienceId = id,
                      Text = key.Item1, Type = key.Type, Question = canonical });
        }
        foreach (var extra in existingQuestions.Skip(update.Questions.Count))
            db.InterviewerExperienceQuestions.Remove(extra);
        AddLog(ModeratedContentType.InterviewerExperience, id, adminId, ModerationAction.Edited, item.Status, item.Status, reason);
        await db.SaveChangesAsync(ct);
        await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return ModerationResult.Success;
    }

    public async Task<ModerationResult> EditCandidateAsync(Guid id, Guid adminId,
        AdminCandidateExperienceUpdateRequest update, CancellationToken ct)
    {
        var item = await db.InterviewExperiences.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || item.Status == ModerationStatus.Deleted) return ModerationResult.NotFound;
        var input = update.Experience;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Remove old round links first, then rounds; one transaction keeps the replacement atomic.
        var stageIds = db.InterviewStages.Where(s => s.InterviewExperienceId == id).Select(s => s.Id);
        await db.InterviewStageQuestions.Where(q => stageIds.Contains(q.InterviewStageId)).ExecuteDeleteAsync(ct);
        await db.InterviewStages.Where(s => s.InterviewExperienceId == id).ExecuteDeleteAsync(ct);
        item.Company = string.IsNullOrWhiteSpace(input.CompanyName) ? null : await GetOrCreateCompanyAsync(input.CompanyName.Trim(), ct);
        item.InterviewDate = input.Stages.Min(s => s.Date);
        item.Level = input.Level;
        item.Outcome = input.Outcome;
        item.IsAuthorAnonymous = input.IsAuthorAnonymous;
        item.Description = input.Description?.Trim();
        item.JobAdText = input.JobAdText?.Trim();
        item.JobAdUrl = input.JobAdUrl?.Trim();
        item.InterviewerConductRating = input.InterviewerConductRating;
        item.JobAdAlignmentRating = input.JobAdAlignmentRating;
        item.RatingReason = input.RatingReason?.Trim();
        var canonicalQuestions = new Dictionary<(string Text, QuestionType Type), Question>();
        for (var stageIndex = 0; stageIndex < input.Stages.Count; stageIndex++)
        {
            var sourceStage = input.Stages[stageIndex];
            var stage = new InterviewStage
            {
                Id = Guid.NewGuid(), Order = stageIndex + 1, Type = sourceStage.Type,
                Date = sourceStage.Date, Description = sourceStage.Description.Trim()
            };
            for (var questionIndex = 0; questionIndex < sourceStage.Questions.Count; questionIndex++)
            {
                var sourceQuestion = sourceStage.Questions[questionIndex];
                Question question;
                if (sourceQuestion.QuestionId is Guid selectedId)
                {
                    question = await db.Questions.SingleOrDefaultAsync(q => q.Id == selectedId, ct)
                        ?? throw new QuestionSelectionException("Selected QuestionId does not exist.");
                    if (question.MergedIntoQuestionId is Guid targetId)
                        question = await db.Questions.SingleAsync(q => q.Id == targetId, ct);
                }
                else
                {
                    var key = (sourceQuestion.Text!.Trim(), sourceQuestion.Type);
                    if (!canonicalQuestions.TryGetValue(key, out question!))
                    {
                        question = await db.Questions.Where(q => q.Text == key.Item1 && q.Type == key.Type)
                            .OrderBy(q => q.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct)
                            ?? new Question { Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type };
                        if (question.MergedIntoQuestionId is Guid targetId)
                            question = await db.Questions.SingleAsync(q => q.Id == targetId, ct);
                        canonicalQuestions.Add(key, question);
                    }
                }
                stage.Questions.Add(new InterviewStageQuestion
                {
                    Id = Guid.NewGuid(), Order = questionIndex + 1,
                    Question = question, PerceivedDifficulty = sourceQuestion.PerceivedDifficulty
                });
            }
            stage.InterviewExperienceId = id;
            db.InterviewStages.Add(stage);
        }
        AddLog(ModeratedContentType.CandidateExperience, id, adminId, ModerationAction.Edited, item.Status, item.Status, update.Reason);
        await db.SaveChangesAsync(ct);
        await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return ModerationResult.Success;
    }

    private async Task<Company> GetOrCreateCompanyAsync(string name, CancellationToken ct)
    {
        var existing = await db.Companies.SingleOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null) return existing;
        var created = new Company { Id = Guid.NewGuid(), Name = name };
        db.Companies.Add(created);
        return created;
    }

    private void AddLog(ModeratedContentType type, Guid contentId, Guid adminId, ModerationAction action,
        ModerationStatus previous, ModerationStatus next, string reason) => db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(), ContentType = type, ContentId = contentId, AdminId = adminId,
            Action = action, PreviousStatus = previous, NewStatus = next,
            Reason = reason.Trim(), CreatedAt = DateTimeOffset.UtcNow
        });
}
