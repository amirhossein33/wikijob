using System.ComponentModel.DataAnnotations;
using InterviewHub.Data.Entities;

namespace InterviewHub.Business.CandidateExperiences;

// Input DTO keeps ownership and moderation fields under server control.
public sealed class CreateCandidateExperienceRequest : IValidatableObject
{
    [MaxLength(200)] public string? CompanyName { get; init; }
    public bool IsAuthorAnonymous { get; init; }
    public ExperienceLevel Level { get; init; } = ExperienceLevel.Unknown;
    public InterviewOutcome Outcome { get; init; } = InterviewOutcome.Unknown;
    [MaxLength(10000)] public string? Description { get; init; }
    [MaxLength(10000)] public string? JobAdText { get; init; }
    [Url, MaxLength(2048)] public string? JobAdUrl { get; init; }
    [Range(1, 5)] public byte? InterviewerConductRating { get; init; }
    [Range(1, 5)] public byte? JobAdAlignmentRating { get; init; }
    [MaxLength(2000)] public string? RatingReason { get; init; }
    [Required, MinLength(1)] public List<CandidateStageRequest> Stages { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Enum.IsDefined(Level) || !Enum.IsDefined(Outcome))
            yield return new("Level or Outcome is invalid.", [nameof(Level), nameof(Outcome)]);
        if (Stages is null || Stages.Count == 0 || Stages.Count > 20)
        {
            yield return new("Provide 1 to 20 interview stages.", [nameof(Stages)]);
            yield break;
        }
        foreach (var stage in Stages)
        {
            if (stage is null || !Enum.IsDefined(stage.Type) || stage.Date == default
                || stage.Date > DateOnly.FromDateTime(DateTime.UtcNow)
                || string.IsNullOrWhiteSpace(stage.Description) || stage.Description.Length > 10000
                || stage.Questions is null || stage.Questions.Count == 0 || stage.Questions.Count > 50
                || stage.Questions.Any(q => q is null ||
                    (q.QuestionId is null && (string.IsNullOrWhiteSpace(q.Text) || q.Text.Length > 2000 || !Enum.IsDefined(q.Type)))
                    || q.QuestionId == Guid.Empty || q.PerceivedDifficulty is < 1 or > 5))
                yield return new("Each stage needs a valid date, type, description, and 1 to 50 valid questions.", [nameof(Stages)]);
        }
    }
}

public sealed class CandidateStageRequest
{
    public InterviewStageType Type { get; init; }
    public DateOnly Date { get; init; }
    [Required, MaxLength(10000)] public string Description { get; init; } = "";
    [Required, MinLength(1)] public List<CandidateQuestionRequest> Questions { get; init; } = [];
}

public sealed class CandidateQuestionRequest
{
    public Guid? QuestionId { get; init; }
    [MaxLength(2000)] public string? Text { get; init; }
    public QuestionType Type { get; init; }
    [Range(1, 5)] public byte? PerceivedDifficulty { get; init; }
}

public sealed class QuestionSelectionException(string message) : Exception(message);

public sealed record CandidateQuestionResponse(Guid QuestionId, string Text, QuestionType Type, byte? PerceivedDifficulty);
public sealed record CandidateStageResponse(Guid Id, int Order, InterviewStageType Type, DateOnly Date,
    string Description, IReadOnlyList<CandidateQuestionResponse> Questions);

// Public and private DTOs differ so an anonymous author ID never reaches public JSON.
public sealed record PublicCandidateExperienceResponse(Guid Id, string? AuthorName, string? CompanyName,
    DateOnly InterviewDate, ExperienceLevel Level, InterviewOutcome Outcome, string? Description,
    string? JobAdText, string? JobAdUrl, byte? InterviewerConductRating,
    byte? JobAdAlignmentRating, string? RatingReason, IReadOnlyList<CandidateStageResponse> Stages);

public sealed record PrivateCandidateExperienceResponse(Guid Id, Guid AuthorId, string AuthorName,
    bool IsAuthorAnonymous, string? CompanyName, DateOnly InterviewDate, ExperienceLevel Level,
    InterviewOutcome Outcome, string? Description, string? JobAdText, string? JobAdUrl,
    byte? InterviewerConductRating, byte? JobAdAlignmentRating, string? RatingReason,
    ModerationStatus Status, IReadOnlyList<CandidateStageResponse> Stages);
