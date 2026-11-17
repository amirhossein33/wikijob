using System.ComponentModel.DataAnnotations;
using InterviewHub.Data.Entities;

namespace InterviewHub.Business.Questions;

// The catalog response contains aggregate counts, never individual experience identities.
public sealed record QuestionResponse(Guid Id, string Text, QuestionType Type, bool IsReferenceQuestion,
    int AskedCount, IReadOnlyList<TagResponse> Tags);
public sealed record TagResponse(Guid Id, string Name);
public sealed record QuestionSearchResponse(int Page, int PageSize, int TotalCount,
    IReadOnlyList<QuestionResponse> Items);
public sealed record QuestionCreatedResponse(Guid Id, bool AlreadyExisted);

public sealed record CreateQuestionRequest([Required, MaxLength(2000)] string Text, QuestionType? Type);
public sealed record CreateTagRequest([Required, MaxLength(80)] string Name);
public sealed record SetQuestionTagsRequest([Required] IReadOnlyList<Guid> TagIds);
public sealed record MergeQuestionRequest(Guid SourceId, Guid TargetId);

public enum QuestionCatalogResult { Success, NotFound, InvalidTag, SameQuestion, TypeMismatch, AlreadyMerged }
