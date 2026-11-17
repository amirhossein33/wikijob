using System.Reflection;
using System.Text.Json;
using InterviewHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Data.Seed;

public static class DbSeeder
{
    private sealed record GeneralQuestion(string Text, QuestionType Type, string[] Tags);

    // General is a reference collection, not a company or an observed interview.
    public static async Task SeedGeneralAsync(InterviewHubDbContext db, CancellationToken ct = default)
    {
        if (await db.Questions.AnyAsync(q => q.ReferenceCollection == "General", ct)) return;
        await using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("InterviewHub.Data.Seed.general-questions.json")
            ?? throw new InvalidOperationException("Embedded General question JSON is missing.");
        var questions = await JsonSerializer.DeserializeAsync<GeneralQuestion[]>(stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            }, ct) ?? throw new InvalidOperationException("General question JSON is empty.");
        if (questions.Length != 100 || questions.Select(q => q.Text).Distinct().Count() != 100)
            throw new InvalidOperationException("General must contain 100 distinct questions.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var tags = await db.Tags.ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var input in questions)
        {
            var question = await db.Questions.Where(q => q.Text == input.Text && q.Type == input.Type)
                .OrderBy(q => q.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct);
            if (question?.MergedIntoQuestionId is Guid targetId)
                question = await db.Questions.SingleAsync(q => q.Id == targetId, ct);
            if (question is null)
            {
                question = new Question { Id = Guid.NewGuid(), Text = input.Text, Type = input.Type };
                db.Questions.Add(question);
            }
            question.IsReferenceQuestion = true;
            question.ReferenceCollection = "General";
            foreach (var tagName in input.Tags)
            {
                if (!tags.TryGetValue(tagName, out var tag))
                {
                    tag = new Tag { Id = Guid.NewGuid(), Name = tagName };
                    db.Tags.Add(tag);
                    tags.Add(tagName, tag);
                }
                var exists = await db.QuestionTags.AnyAsync(qt => qt.QuestionId == question.Id && qt.TagId == tag.Id, ct);
                if (!exists && !question.QuestionTags.Any(qt => qt.TagId == tag.Id))
                    question.QuestionTags.Add(new QuestionTag { Question = question, Tag = tag });
            }
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    // Existing interviewer submissions predate the canonical QuestionId column.
    public static async Task BackfillInterviewerQuestionLinksAsync(InterviewHubDbContext db, CancellationToken ct = default)
    {
        var unlinked = await db.InterviewerExperienceQuestions.Where(q => q.QuestionId == null).ToListAsync(ct);
        if (unlinked.Count == 0) return;
        var cache = new Dictionary<(string Text, QuestionType Type), Question>();
        foreach (var row in unlinked)
        {
            var key = (row.Text.Trim(), row.Type);
            if (!cache.TryGetValue(key, out var question))
            {
                question = await db.Questions.Where(q => q.Text == key.Item1 && q.Type == key.Type)
                    .OrderBy(q => q.MergedIntoQuestionId == null ? 0 : 1).FirstOrDefaultAsync(ct);
                if (question is null)
                {
                    question = new Question { Id = Guid.NewGuid(), Text = key.Item1, Type = key.Type };
                    db.Questions.Add(question);
                }
                if (question.MergedIntoQuestionId is Guid targetId)
                    question = await db.Questions.SingleAsync(q => q.Id == targetId, ct);
                cache.Add(key, question);
            }
            row.Question = question;
        }
        await db.SaveChangesAsync(ct);
    }
}
