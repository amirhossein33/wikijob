using InterviewHub.Data;
using InterviewHub.Data.Entities;
using InterviewHub.Business.Reports;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.Questions;

public sealed class QuestionCatalogService(InterviewHubDbContext db, ReportSummaryService reports)
{
    // Public search includes reference questions and questions from published candidate experiences.
    public async Task<QuestionSearchResponse> SearchAsync(string? text, QuestionType? type, Guid? tagId,
        int page, int pageSize, CancellationToken ct)
    {
        var query = VisibleQuestions();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(q => q.Text.Contains(text.Trim()));
        if (type is not null) query = query.Where(q => q.Type == type);
        if (tagId is not null) query = query.Where(q => q.QuestionTags.Any(qt => qt.TagId == tagId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(q => q.Text).ThenBy(q => q.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(q => q.QuestionTags).ThenInclude(qt => qt.Tag)
            .AsSplitQuery().ToListAsync(ct);
        return new QuestionSearchResponse(page, pageSize, total, await MapWithCountsAsync(items, ct));
    }

    public async Task<QuestionResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var alias = await db.Questions.AsNoTracking().Where(q => q.Id == id)
            .Select(q => q.MergedIntoQuestionId).SingleOrDefaultAsync(ct);
        var canonicalId = alias ?? id;
        var item = await VisibleQuestions().Include(q => q.QuestionTags).ThenInclude(qt => qt.Tag)
            .SingleOrDefaultAsync(q => q.Id == canonicalId, ct);
        if (item is null) return null;
        return (await MapWithCountsAsync([item], ct))[0];
    }

    public async Task<IReadOnlyList<TagResponse>> TagsAsync(CancellationToken ct) =>
        await db.Tags.AsNoTracking().OrderBy(t => t.Name).Select(t => new TagResponse(t.Id, t.Name)).ToListAsync(ct);

    // Exact matches reuse a canonical ID; near duplicates remain for manual review.
    public async Task<QuestionCreatedResponse> CreateQuestionAsync(CreateQuestionRequest request, CancellationToken ct)
    {
        var text = request.Text.Trim();
        var existing = await db.Questions.AsNoTracking()
            .Where(q => q.Text == text && q.Type == request.Type!.Value)
            .OrderBy(q => q.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct);
        if (existing is not null)
            return new QuestionCreatedResponse(existing.MergedIntoQuestionId ?? existing.Id, true);
        var question = new Question { Id = Guid.NewGuid(), Text = text, Type = request.Type!.Value };
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);
        return new QuestionCreatedResponse(question.Id, false);
    }

    public async Task<TagResponse> CreateTagAsync(CreateTagRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var existing = await db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Name == name, ct);
        if (existing is not null) return new TagResponse(existing.Id, existing.Name);
        var tag = new Tag { Id = Guid.NewGuid(), Name = name };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return new TagResponse(tag.Id, tag.Name);
    }

    // Replace tag links by difference to avoid duplicate composite-key tracking.
    public async Task<QuestionCatalogResult> SetTagsAsync(Guid questionId, IReadOnlyList<Guid> tagIds, CancellationToken ct)
    {
        var question = await db.Questions.Include(q => q.QuestionTags).SingleOrDefaultAsync(q => q.Id == questionId, ct);
        if (question is null || question.MergedIntoQuestionId is not null) return QuestionCatalogResult.NotFound;
        var unique = tagIds.Distinct().ToHashSet();
        if (await db.Tags.CountAsync(t => unique.Contains(t.Id), ct) != unique.Count)
            return QuestionCatalogResult.InvalidTag;
        foreach (var link in question.QuestionTags.Where(qt => !unique.Contains(qt.TagId)).ToArray())
            db.QuestionTags.Remove(link);
        var existing = question.QuestionTags.Select(qt => qt.TagId).ToHashSet();
        foreach (var id in unique.Where(id => !existing.Contains(id)))
            db.QuestionTags.Add(new QuestionTag { QuestionId = questionId, TagId = id });
        await db.SaveChangesAsync(ct);
        return QuestionCatalogResult.Success;
    }

    // Merge moves occurrences and tags, retaining the source as a resolvable alias.
    public async Task<QuestionCatalogResult> MergeAsync(MergeQuestionRequest request, CancellationToken ct)
    {
        if (request.SourceId == request.TargetId) return QuestionCatalogResult.SameQuestion;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var source = await db.Questions.Include(q => q.QuestionTags).SingleOrDefaultAsync(q => q.Id == request.SourceId, ct);
        var target = await db.Questions.Include(q => q.QuestionTags).SingleOrDefaultAsync(q => q.Id == request.TargetId, ct);
        if (source is null || target is null) return QuestionCatalogResult.NotFound;
        if (source.MergedIntoQuestionId is not null || target.MergedIntoQuestionId is not null)
            return QuestionCatalogResult.AlreadyMerged;
        if (source.Type != target.Type) return QuestionCatalogResult.TypeMismatch;

        var targetTags = target.QuestionTags.Select(qt => qt.TagId).ToHashSet();
        foreach (var tag in source.QuestionTags.Where(qt => !targetTags.Contains(qt.TagId)))
            db.QuestionTags.Add(new QuestionTag { QuestionId = target.Id, TagId = tag.TagId });
        target.IsReferenceQuestion |= source.IsReferenceQuestion;

        var stageLinks = await db.InterviewStageQuestions.Where(q => q.QuestionId == source.Id).ToListAsync(ct);
        foreach (var link in stageLinks) link.QuestionId = target.Id;
        var interviewerLinks = await db.InterviewerExperienceQuestions.Where(q => q.QuestionId == source.Id).ToListAsync(ct);
        foreach (var link in interviewerLinks) link.QuestionId = target.Id;

        // Legacy links have a composite key and must be removed before replacement.
        var legacy = await db.ExperienceQuestions.Where(q => q.QuestionId == source.Id).ToListAsync(ct);
        var existingTargetExperienceIds = await db.ExperienceQuestions.Where(q => q.QuestionId == target.Id)
            .Select(q => q.InterviewExperienceId).ToListAsync(ct);
        db.ExperienceQuestions.RemoveRange(legacy);
        await db.SaveChangesAsync(ct);
        foreach (var link in legacy.Where(q => !existingTargetExperienceIds.Contains(q.InterviewExperienceId)))
            db.ExperienceQuestions.Add(new ExperienceQuestion
            {
                InterviewExperienceId = link.InterviewExperienceId,
                QuestionId = target.Id, PerceivedDifficulty = link.PerceivedDifficulty
            });
        // Older aliases must keep resolving directly to the final canonical question.
        var aliases = await db.Questions.Where(q => q.MergedIntoQuestionId == source.Id).ToListAsync(ct);
        foreach (var alias in aliases) alias.MergedIntoQuestionId = target.Id;
        source.MergedIntoQuestionId = target.Id;
        await db.SaveChangesAsync(ct);
        await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return QuestionCatalogResult.Success;
    }

    private IQueryable<Question> VisibleQuestions() => db.Questions.AsNoTracking()
        .Where(q => q.MergedIntoQuestionId == null &&
            (q.IsReferenceQuestion || q.StageQuestions.Any(s => s.Stage.InterviewExperience.Status == ModerationStatus.Published)
                || q.InterviewerQuestions.Any(i => i.InterviewerExperience.Status == ModerationStatus.Published)
                || q.ExperienceQuestions.Any(e => e.InterviewExperience.Status == ModerationStatus.Published)));

    private async Task<IReadOnlyList<QuestionResponse>> MapWithCountsAsync(IReadOnlyList<Question> items, CancellationToken ct)
    {
        if (items.Count == 0) return [];
        var ids = items.Select(q => q.Id).ToArray();
        var stagePairs = await db.InterviewStageQuestions.AsNoTracking()
            .Where(q => ids.Contains(q.QuestionId) && q.Stage.InterviewExperience.Status == ModerationStatus.Published)
            .Select(q => new { q.QuestionId, q.Stage.InterviewExperienceId }).Distinct().ToListAsync(ct);
        var legacyPairs = await db.ExperienceQuestions.AsNoTracking()
            .Where(q => ids.Contains(q.QuestionId) && q.InterviewExperience.Status == ModerationStatus.Published)
            .Select(q => new { q.QuestionId, q.InterviewExperienceId }).Distinct().ToListAsync(ct);
        var counts = stagePairs.Concat(legacyPairs).Distinct()
            .GroupBy(x => x.QuestionId).ToDictionary(g => g.Key, g => g.Count());
        return items.Select(q => new QuestionResponse(q.Id, q.Text, q.Type, q.IsReferenceQuestion,
            counts.GetValueOrDefault(q.Id), q.QuestionTags.Select(qt => new TagResponse(qt.TagId, qt.Tag.Name))
                .OrderBy(t => t.Name).ToArray())).ToArray();
    }
}
