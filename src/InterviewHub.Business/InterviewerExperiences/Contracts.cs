using System.ComponentModel.DataAnnotations;
using InterviewHub.Data.Entities;

namespace InterviewHub.Business.InterviewerExperiences;

// DTOs validate submitted fields without exposing the persisted author ID or status.
public sealed class CreateInterviewerExperienceRequest : IValidatableObject
{
    [Required, MaxLength(200)] public string InterviewerName { get; init; } = "";
    [Required, MaxLength(200)] public string CompanyName { get; init; } = "";
    public bool ShowInterviewerName { get; init; }
    public bool ShowCompanyName { get; init; }
    public DateOnly InterviewDate { get; init; }
    public ExperienceLevel Level { get; init; }
    [Required, MaxLength(10000)] public string Description { get; init; } = "";
    [MaxLength(10000)] public string? JobAdText { get; init; }
    [Url, MaxLength(2048)] public string? JobAdUrl { get; init; }
    [Range(1, 5)] public byte InterviewRating { get; init; }
    [Required, MinLength(1)] public List<InterviewerQuestionRequest> Questions { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(InterviewerName)) yield return new("InterviewerName is required.", [nameof(InterviewerName)]);
        if (string.IsNullOrWhiteSpace(CompanyName)) yield return new("CompanyName is required.", [nameof(CompanyName)]);
        if (string.IsNullOrWhiteSpace(Description)) yield return new("Description is required.", [nameof(Description)]);
        if (InterviewDate == default || InterviewDate > DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new("InterviewDate must be a valid past or current date.", [nameof(InterviewDate)]);
        if (!Enum.IsDefined(Level) || Level == ExperienceLevel.Unknown)
            yield return new("A known Level is required.", [nameof(Level)]);
        if (Questions is null || Questions.Count == 0 || Questions.Any(q => string.IsNullOrWhiteSpace(q.Text) || q.Text.Length > 2000 || !Enum.IsDefined(q.Type)))
            yield return new("At least one valid question is required.", [nameof(Questions)]);
    }
}

public sealed class InterviewerQuestionRequest
{
    [Required, MaxLength(2000)] public string Text { get; init; } = "";
    public QuestionType Type { get; init; }
}

// Public DTO deliberately omits author ID and any fields hidden by visibility settings.
public sealed record PublicInterviewerExperienceResponse(
    Guid Id, string? InterviewerName, string? CompanyName, DateOnly InterviewDate,
    ExperienceLevel Level, string Description, string? JobAdText, string? JobAdUrl,
    byte InterviewRating, IReadOnlyList<InterviewerQuestionResponse> Questions);

public sealed record InterviewerQuestionResponse(string Text, QuestionType Type);

// The authenticated owner and Admin can inspect the complete stored submission.
public sealed record PrivateInterviewerExperienceResponse(
    Guid Id, string InterviewerName, string CompanyName, bool ShowInterviewerName,
    bool ShowCompanyName, DateOnly InterviewDate, ExperienceLevel Level,
    string Description, string? JobAdText, string? JobAdUrl, byte InterviewRating,
    ModerationStatus Status, IReadOnlyList<InterviewerQuestionResponse> Questions);
