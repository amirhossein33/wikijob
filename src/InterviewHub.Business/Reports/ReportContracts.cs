using InterviewHub.Data.Entities;

namespace InterviewHub.Business.Reports;

// Shared report filters map directly to the SQL summary dimensions.
public sealed record ReportFilterDto(Guid? CompanyId, ExperienceLevel? Level,
    QuestionType? Type, Guid? TagId, DateOnly? From, DateOnly? To);

public sealed record TopQuestionRow(Guid QuestionId, string Text, QuestionType Type, int ExperienceCount);
public sealed record TypeDistributionRow(QuestionType Type, int QuestionMentions);
public sealed record TopTagRow(Guid TagId, string Name, int QuestionMentions);
public sealed record MonthlyTrendRow(int Year, int Month, int Count);
public sealed record MarketReportResponse(DateTimeOffset? RefreshedAt,
    IReadOnlyList<TopQuestionRow> TopQuestions, IReadOnlyList<TypeDistributionRow> TypeDistribution,
    IReadOnlyList<TopTagRow> TopTags, string TrendMetric, IReadOnlyList<MonthlyTrendRow> Trend);

// Null rates mean fewer than five qualifying answers, not zero percent or zero stars.
public sealed record CompanyReportResponse(Guid CompanyId, bool Available, int? ExperienceCount,
    double? PassRate, int? KnownOutcomeCount, double? ConductRating, int? ConductRatingCount,
    double? AdAlignmentRating, int? AdAlignmentRatingCount,
    IReadOnlyList<TopQuestionRow> TopQuestions, IReadOnlyDictionary<int, int> DifficultyDistribution);
public sealed record CompanyComparisonResponse(CompanyReportResponse Left, CompanyReportResponse Right);
public sealed record PriorityReportRow(string Skill, int ProfileCount, double AverageImportance);
