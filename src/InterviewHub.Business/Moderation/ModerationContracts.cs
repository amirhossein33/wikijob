using System.ComponentModel.DataAnnotations;
using InterviewHub.Data.Entities;
using InterviewHub.Business.InterviewerExperiences;
using InterviewHub.Business.CandidateExperiences;

namespace InterviewHub.Business.Moderation;

// Admin requests carry an explicit reason for every moderation decision.
public sealed record ModerationReasonRequest([Required, MinLength(3), MaxLength(2000)] string Reason);

public sealed record PendingExperienceResponse(Guid Id, ModeratedContentType Type, DateOnly InterviewDate, DateTimeOffset CreatedAt);
public sealed record ModerationLogResponse(Guid Id, ModerationAction Action, ModerationStatus PreviousStatus,
    ModerationStatus NewStatus, string Reason, DateTimeOffset CreatedAt, Guid AdminId);

public sealed record AdminInterviewerExperienceUpdateRequest(
    [Required] CreateInterviewerExperienceRequest Experience,
    [Required, MinLength(3), MaxLength(2000)] string Reason);

// Admin replacement uses the same validated shape as candidate submission.
public sealed record AdminCandidateExperienceUpdateRequest(
    [Required] CreateCandidateExperienceRequest Experience,
    [Required, MinLength(3), MaxLength(2000)] string Reason);
