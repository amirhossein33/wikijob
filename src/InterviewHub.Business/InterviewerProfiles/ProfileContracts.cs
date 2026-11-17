using System.ComponentModel.DataAnnotations;
using InterviewHub.Data.Entities;

namespace InterviewHub.Business.InterviewerProfiles;

// Independent switches control public name, company, and biography.
public sealed class SaveInterviewerProfileRequest : IValidatableObject
{
    [Required, MaxLength(200)] public string DisplayName { get; init; } = "";
    [MaxLength(200)] public string? CompanyName { get; init; }
    [MaxLength(2000)] public string? Bio { get; init; }
    [MaxLength(5000)] public string? AdviceText { get; init; }
    public bool ShowName { get; init; }
    public bool ShowCompany { get; init; }
    public bool ShowBio { get; init; }
    [Required, MinLength(1)] public List<PriorityRequest> Priorities { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
            yield return new("DisplayName is required.", [nameof(DisplayName)]);
        if (Priorities is null || Priorities.Count is < 1 or > 30)
        {
            yield return new("Provide 1 to 30 priorities.", [nameof(Priorities)]);
            yield break;
        }
        if (Priorities.Any(p => p is null || string.IsNullOrWhiteSpace(p.Skill) || p.Skill.Length > 120
            || p.Importance is < 1 or > 5 || p.Note?.Length > 1000))
        {
            yield return new("Every priority needs a skill and importance from 1 to 5.", [nameof(Priorities)]);
            yield break;
        }
        if (Priorities.Where(p => p is not null).Select(p => p.Skill.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Priorities.Count)
            yield return new("Priority skills must be unique.", [nameof(Priorities)]);
    }
}

public sealed record PriorityRequest([Required, MaxLength(120)] string Skill, [Range(1, 5)] byte Importance,
    [MaxLength(1000)] string? Note);
public sealed record PriorityResponse(string Skill, byte Importance, string? Note);

// The public DTO never includes UserId or values behind disabled visibility switches.
public sealed record PublicInterviewerProfileResponse(Guid Id, string? DisplayName, string? CompanyName,
    string? Bio, string? AdviceText, IReadOnlyList<PriorityResponse> Priorities);

// Owner and Admin can review all stored fields and the moderation status.
public sealed record PrivateInterviewerProfileResponse(Guid UserId, Guid PublicId, string DisplayName,
    string? CompanyName, string? Bio, string? AdviceText, bool ShowName, bool ShowCompany,
    bool ShowBio, ModerationStatus Status, IReadOnlyList<PriorityResponse> Priorities);

public sealed record PendingInterviewerProfileResponse(Guid UserId, Guid PublicId, DateTimeOffset UpdatedAt);
public enum ProfileSaveResult { Saved, Deleted }
