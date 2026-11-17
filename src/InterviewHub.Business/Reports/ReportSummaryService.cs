using InterviewHub.Data;
using InterviewHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.Reports;

public sealed class ReportSummaryService(InterviewHubDbContext db)
{
    // Rebuild physical SQL tables from published data; the transaction prevents partial snapshots.
    public async Task RebuildAsync(CancellationToken ct)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        var experiences = await db.InterviewExperiences.AsNoTracking()
            .Where(x => x.Status == ModerationStatus.Published)
            .Include(x => x.Stages).ThenInclude(s => s.Questions)
            .Include(x => x.ExperienceQuestions).AsSplitQuery().ToListAsync(ct);
        var profiles = await db.InterviewerProfiles.AsNoTracking()
            .Where(x => x.Status == ModerationStatus.Published)
            .Include(x => x.Priorities).ToListAsync(ct);

        var occurrenceRows = experiences.SelectMany(experience =>
        {
            var occurrences = experience.Stages.SelectMany(s => s.Questions)
                .Select(q => (q.QuestionId, q.PerceivedDifficulty))
                .Concat(experience.ExperienceQuestions.Select(q => (q.QuestionId, q.PerceivedDifficulty)));
            return occurrences.GroupBy(q => q.QuestionId).Select(group =>
                new { experience.InterviewDate, experience.CompanyId, experience.Level,
                    QuestionId = group.Key,
                    Difficulty = group.Select(q => q.PerceivedDifficulty).FirstOrDefault(d => d is not null) });
        }).ToArray();

        var questionRows = occurrenceRows.GroupBy(x => new { x.InterviewDate, x.CompanyId, x.Level, x.QuestionId })
            .Select(group => new QuestionDailySummary
            {
                Id = Guid.NewGuid(), Day = group.Key.InterviewDate,
                CompanyId = group.Key.CompanyId, Level = group.Key.Level,
                QuestionId = group.Key.QuestionId, ExperienceCount = group.Count(),
                DifficultyCount = group.Count(x => x.Difficulty is not null),
                DifficultySum = group.Sum(x => (int?)x.Difficulty ?? 0),
                Difficulty1Count = group.Count(x => x.Difficulty == 1),
                Difficulty2Count = group.Count(x => x.Difficulty == 2),
                Difficulty3Count = group.Count(x => x.Difficulty == 3),
                Difficulty4Count = group.Count(x => x.Difficulty == 4),
                Difficulty5Count = group.Count(x => x.Difficulty == 5)
            }).ToArray();

        var companyRows = experiences.GroupBy(x => new { x.InterviewDate, x.CompanyId, x.Level })
            .Select(group => new CompanyDailySummary
            {
                Id = Guid.NewGuid(), Day = group.Key.InterviewDate,
                CompanyId = group.Key.CompanyId, Level = group.Key.Level,
                ExperienceCount = group.Count(),
                PassedCount = group.Count(x => x.Outcome == InterviewOutcome.Passed),
                RejectedCount = group.Count(x => x.Outcome == InterviewOutcome.Rejected),
                ConductRatingCount = group.Count(x => x.InterviewerConductRating is not null),
                ConductRatingSum = group.Sum(x => (int?)x.InterviewerConductRating ?? 0),
                AdAlignmentRatingCount = group.Count(x => x.JobAdAlignmentRating is not null),
                AdAlignmentRatingSum = group.Sum(x => (int?)x.JobAdAlignmentRating ?? 0)
            }).ToArray();

        var priorityRows = profiles.SelectMany(p => p.Priorities)
            .GroupBy(p => p.Skill.Trim().ToUpperInvariant())
            .Select(group => new InterviewerPrioritySummary
            {
                Id = Guid.NewGuid(), NormalizedSkill = group.Key,
                Skill = group.First().Skill.Trim(), ProfileCount = group.Count(),
                ImportanceSum = group.Sum(p => (int)p.Importance)
            }).ToArray();

        await db.QuestionDailySummaries.ExecuteDeleteAsync(ct);
        await db.CompanyDailySummaries.ExecuteDeleteAsync(ct);
        await db.InterviewerPrioritySummaries.ExecuteDeleteAsync(ct);
        db.QuestionDailySummaries.AddRange(questionRows);
        db.CompanyDailySummaries.AddRange(companyRows);
        db.InterviewerPrioritySummaries.AddRange(priorityRows);
        var state = await db.ReportRefreshStates.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (state is null)
            db.ReportRefreshStates.Add(new ReportRefreshState { LastRefreshedAt = DateTimeOffset.UtcNow });
        else state.LastRefreshedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        if (ownsTransaction) await transaction!.CommitAsync(ct);
    }
}
