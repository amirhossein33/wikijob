using Microsoft.AspNetCore.Identity;

namespace InterviewHub.Data.Entities;

// A single Identity account can hold both candidate and interviewer roles.
public class AppUser : IdentityUser<Guid>
{
    public int? YearsOfExperience { get; set; }
    public InterviewerProfile? InterviewerProfile { get; set; }
    public ICollection<InterviewExperience> Experiences { get; set; } = [];
    public ICollection<InterviewerExperience> InterviewerExperiences { get; set; } = [];
}

// Company ratings are derived from published experiences rather than stored here.
public class Company
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<InterviewExperience> Experiences { get; set; } = [];
    public ICollection<InterviewerExperience> InterviewerExperiences { get; set; } = [];
}

// The interviewer owns the complete record; visibility flags affect only public responses.
public class InterviewerExperience
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = null!;
    public required string InterviewerName { get; set; }
    public bool ShowInterviewerName { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public bool ShowCompanyName { get; set; }
    public DateOnly InterviewDate { get; set; }
    public ExperienceLevel Level { get; set; }
    public required string Description { get; set; }
    public string? JobAdText { get; set; }
    public string? JobAdUrl { get; set; }
    public byte InterviewRating { get; set; }
    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<InterviewerExperienceQuestion> Questions { get; set; } = [];
}

// Questions are stored as written until canonical question matching is introduced.
public class InterviewerExperienceQuestion
{
    public Guid Id { get; set; }
    public Guid InterviewerExperienceId { get; set; }
    public InterviewerExperience InterviewerExperience { get; set; } = null!;
    public required string Text { get; set; }
    public QuestionType Type { get; set; }
    public Guid? QuestionId { get; set; }
    public Question? Question { get; set; }
}

public enum QuestionType { Algorithm, LiveCode, Conceptual, SystemDesign }
public enum ExperienceLevel { Unknown, Intern, Junior, Mid, Senior, Lead }
public enum InterviewOutcome { Unknown, Passed, Rejected }
public enum InterviewStageType { HR, Technical, LiveCoding, SystemDesign, Other }
public enum ModerationStatus { Pending, Published, Hidden, Rejected, Deleted }
public enum ReportStatus { Open, Resolved, Dismissed }
public enum ModeratedContentType { CandidateExperience, InterviewerExperience, InterviewerProfile }
public enum ModerationAction { Published, Rejected, Edited, Deleted }

// An append-only record identifies who changed publication state or content and why.
public class ModerationLog
{
    public Guid Id { get; set; }
    public ModeratedContentType ContentType { get; set; }
    public Guid ContentId { get; set; }
    public Guid AdminId { get; set; }
    public AppUser Admin { get; set; } = null!;
    public ModerationAction Action { get; set; }
    public ModerationStatus PreviousStatus { get; set; }
    public ModerationStatus NewStatus { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// Reference questions do not count as interview occurrences until linked to an experience.
public class Question
{
    public Guid Id { get; set; }
    public required string Text { get; set; }
    public QuestionType Type { get; set; }
    public bool IsReferenceQuestion { get; set; }
    public string? ReferenceCollection { get; set; }
    public Guid? MergedIntoQuestionId { get; set; }
    public Question? MergedIntoQuestion { get; set; }
    public ICollection<QuestionTag> QuestionTags { get; set; } = [];
    public ICollection<ExperienceQuestion> ExperienceQuestions { get; set; } = [];
    public ICollection<InterviewStageQuestion> StageQuestions { get; set; } = [];
    public ICollection<InterviewerExperienceQuestion> InterviewerQuestions { get; set; } = [];
}

// Tags support question discovery and reporting.
public class Tag
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<QuestionTag> QuestionTags { get; set; } = [];
}

public class QuestionTag
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

// Author ownership is private; public presentation obeys IsAuthorAnonymous.
public class InterviewExperience
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = null!;
    public bool IsAuthorAnonymous { get; set; }
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public DateOnly InterviewDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ExperienceLevel Level { get; set; } = ExperienceLevel.Unknown;
    public InterviewOutcome Outcome { get; set; } = InterviewOutcome.Unknown;
    public string? JobAdText { get; set; }
    public string? JobAdUrl { get; set; }
    public string? Description { get; set; }
    public byte? InterviewerConductRating { get; set; }
    public byte? JobAdAlignmentRating { get; set; }
    public string? RatingReason { get; set; }
    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;
    public ICollection<ExperienceQuestion> ExperienceQuestions { get; set; } = [];
    public ICollection<InterviewStage> Stages { get; set; } = [];
    public ICollection<ContentReport> Reports { get; set; } = [];
}

// Each round has its own date, type, description, and ordered questions.
public class InterviewStage
{
    public Guid Id { get; set; }
    public Guid InterviewExperienceId { get; set; }
    public InterviewExperience InterviewExperience { get; set; } = null!;
    public int Order { get; set; }
    public InterviewStageType Type { get; set; }
    public DateOnly Date { get; set; }
    public required string Description { get; set; }
    public ICollection<InterviewStageQuestion> Questions { get; set; } = [];
}

// A canonical question can appear in several rounds; reports can count distinct experiences.
public class InterviewStageQuestion
{
    public Guid Id { get; set; }
    public Guid InterviewStageId { get; set; }
    public InterviewStage Stage { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int Order { get; set; }
    public byte? PerceivedDifficulty { get; set; }
}

// This link records actual question occurrence and perceived difficulty.
public class ExperienceQuestion
{
    public Guid InterviewExperienceId { get; set; }
    public InterviewExperience InterviewExperience { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public byte? PerceivedDifficulty { get; set; }
}

// Each public profile field has an independent visibility switch.
public class InterviewerProfile
{
    public Guid UserId { get; set; }
    public Guid PublicId { get; set; }
    public AppUser User { get; set; } = null!;
    public required string DisplayName { get; set; }
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string? Bio { get; set; }
    public string? AdviceText { get; set; }
    public bool ShowName { get; set; }
    public bool ShowCompany { get; set; }
    public bool ShowBio { get; set; }
    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<InterviewerPriority> Priorities { get; set; } = [];
}

// Interviewer priorities can later be aggregated without exposing private profiles.
public class InterviewerPriority
{
    public Guid Id { get; set; }
    public Guid InterviewerUserId { get; set; }
    public InterviewerProfile InterviewerProfile { get; set; } = null!;
    public required string Skill { get; set; }
    public byte Importance { get; set; }
    public string? Note { get; set; }
}

// Reports give moderators a review trail for published experience content.
public class ContentReport
{
    public Guid Id { get; set; }
    public Guid InterviewExperienceId { get; set; }
    public InterviewExperience InterviewExperience { get; set; } = null!;
    public Guid ReporterId { get; set; }
    public AppUser Reporter { get; set; } = null!;
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
}

// SQL summary rows are rebuilt from published candidate experiences after content changes.
public class QuestionDailySummary
{
    public Guid Id { get; set; }
    public DateOnly Day { get; set; }
    public Guid? CompanyId { get; set; }
    public ExperienceLevel Level { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int ExperienceCount { get; set; }
    public int DifficultyCount { get; set; }
    public int DifficultySum { get; set; }
    public int Difficulty1Count { get; set; }
    public int Difficulty2Count { get; set; }
    public int Difficulty3Count { get; set; }
    public int Difficulty4Count { get; set; }
    public int Difficulty5Count { get; set; }
}

// Company outcomes and rating sums retain denominators for honest averages.
public class CompanyDailySummary
{
    public Guid Id { get; set; }
    public DateOnly Day { get; set; }
    public Guid? CompanyId { get; set; }
    public ExperienceLevel Level { get; set; }
    public int ExperienceCount { get; set; }
    public int PassedCount { get; set; }
    public int RejectedCount { get; set; }
    public int ConductRatingCount { get; set; }
    public int ConductRatingSum { get; set; }
    public int AdAlignmentRatingCount { get; set; }
    public int AdAlignmentRatingSum { get; set; }
}

// Only priorities from published interviewer profiles contribute to this table.
public class InterviewerPrioritySummary
{
    public Guid Id { get; set; }
    public required string Skill { get; set; }
    public required string NormalizedSkill { get; set; }
    public int ProfileCount { get; set; }
    public int ImportanceSum { get; set; }
}

public class ReportRefreshState
{
    public int Id { get; set; }
    public DateTimeOffset LastRefreshedAt { get; set; }
}
