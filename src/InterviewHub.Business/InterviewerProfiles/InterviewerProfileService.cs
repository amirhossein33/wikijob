using InterviewHub.Data;
using InterviewHub.Data.Entities;
using InterviewHub.Business.Reports;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Business.InterviewerProfiles;

public sealed class InterviewerProfileService(InterviewHubDbContext db, ReportSummaryService reports)
{
    // Every owner edit returns the profile to Pending for fresh admin review.
    public async Task<(ProfileSaveResult Result, PrivateInterviewerProfileResponse? Profile)> SaveAsync(
        Guid userId, SaveInterviewerProfileRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.InterviewerProfiles.Include(x => x.Company).Include(x => x.Priorities)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (item?.Status == ModerationStatus.Deleted) return (ProfileSaveResult.Deleted, null);
        if (item is null)
        {
            item = new InterviewerProfile
            {
                UserId = userId, PublicId = Guid.NewGuid(), DisplayName = request.DisplayName.Trim()
            };
            db.InterviewerProfiles.Add(item);
        }
        item.DisplayName = request.DisplayName.Trim();
        item.Company = string.IsNullOrWhiteSpace(request.CompanyName)
            ? null : await GetOrCreateCompanyAsync(request.CompanyName.Trim(), ct);
        item.Bio = request.Bio?.Trim();
        item.AdviceText = request.AdviceText?.Trim();
        item.ShowName = request.ShowName;
        item.ShowCompany = request.ShowCompany;
        item.ShowBio = request.ShowBio;
        item.Status = ModerationStatus.Pending;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        // Reconcile child rows by skill to preserve IDs and avoid duplicate tracked keys.
        var requested = request.Priorities.ToDictionary(p => p.Skill.Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var current in item.Priorities.ToArray())
        {
            if (requested.TryGetValue(current.Skill, out var priority))
            {
                current.Importance = priority.Importance;
                current.Note = priority.Note?.Trim();
                current.Skill = priority.Skill.Trim();
            }
            else db.InterviewerPriorities.Remove(current);
        }
        var existingSkills = item.Priorities.Select(p => p.Skill).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var priority in request.Priorities.Where(p => !existingSkills.Contains(p.Skill.Trim())))
            db.InterviewerPriorities.Add(new InterviewerPriority
            {
                Id = Guid.NewGuid(), InterviewerUserId = userId, Skill = priority.Skill.Trim(),
                Importance = priority.Importance, Note = priority.Note?.Trim()
            });
        await db.SaveChangesAsync(ct);
        await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return (ProfileSaveResult.Saved, ToPrivate(item));
    }

    public async Task<PrivateInterviewerProfileResponse?> GetPrivateAsync(Guid userId, CancellationToken ct)
    {
        var item = await Query().SingleOrDefaultAsync(x => x.UserId == userId && x.Status != ModerationStatus.Deleted, ct);
        return item is null ? null : ToPrivate(item);
    }

    public async Task<PublicInterviewerProfileResponse?> GetPublicAsync(Guid publicId, CancellationToken ct)
    {
        var item = await Query().SingleOrDefaultAsync(x => x.PublicId == publicId && x.Status == ModerationStatus.Published, ct);
        return item is null ? null : new PublicInterviewerProfileResponse(item.PublicId,
            item.ShowName ? item.DisplayName : null, item.ShowCompany ? item.Company?.Name : null,
            item.ShowBio ? item.Bio : null, item.AdviceText, ToPriorities(item));
    }

    public async Task<IReadOnlyList<PendingInterviewerProfileResponse>> PendingAsync(int take, CancellationToken ct) =>
        await db.InterviewerProfiles.AsNoTracking().Where(x => x.Status == ModerationStatus.Pending)
            .OrderBy(x => x.UpdatedAt).Take(take)
            .Select(x => new PendingInterviewerProfileResponse(x.UserId, x.PublicId, x.UpdatedAt)).ToListAsync(ct);

    // Admin decisions share the existing append-only moderation log table.
    public async Task<bool> ModerateAsync(Guid userId, Guid adminId, ModerationAction action,
        string reason, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.InterviewerProfiles.FindAsync([userId], ct);
        if (item is null || item.Status == ModerationStatus.Deleted) return false;
        var previous = item.Status;
        item.Status = action switch
        {
            ModerationAction.Published => ModerationStatus.Published,
            ModerationAction.Rejected => ModerationStatus.Rejected,
            ModerationAction.Deleted => ModerationStatus.Deleted,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(), ContentType = ModeratedContentType.InterviewerProfile,
            ContentId = userId, AdminId = adminId, Action = action,
            PreviousStatus = previous, NewStatus = item.Status,
            Reason = reason.Trim(), CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await reports.RebuildAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private IQueryable<InterviewerProfile> Query() => db.InterviewerProfiles.AsNoTracking()
        .Include(x => x.Company).Include(x => x.Priorities);

    private static PrivateInterviewerProfileResponse ToPrivate(InterviewerProfile item) =>
        new(item.UserId, item.PublicId, item.DisplayName, item.Company?.Name,
            item.Bio, item.AdviceText, item.ShowName, item.ShowCompany, item.ShowBio,
            item.Status, ToPriorities(item));

    private static IReadOnlyList<PriorityResponse> ToPriorities(InterviewerProfile item) =>
        item.Priorities.OrderByDescending(p => p.Importance).ThenBy(p => p.Skill)
            .Select(p => new PriorityResponse(p.Skill, p.Importance, p.Note)).ToArray();

    private async Task<Company> GetOrCreateCompanyAsync(string name, CancellationToken ct)
    {
        var existing = await db.Companies.SingleOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null) return existing;
        var company = new Company { Id = Guid.NewGuid(), Name = name };
        db.Companies.Add(company);
        return company;
    }
}
