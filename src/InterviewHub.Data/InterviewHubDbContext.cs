using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InterviewHub.Data;

// Identity and interview data share one EF Core model and migration history.
public class InterviewHubDbContext(DbContextOptions<InterviewHubDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<QuestionTag> QuestionTags => Set<QuestionTag>();
    public DbSet<InterviewExperience> InterviewExperiences => Set<InterviewExperience>();
    public DbSet<ExperienceQuestion> ExperienceQuestions => Set<ExperienceQuestion>();
    public DbSet<InterviewerProfile> InterviewerProfiles => Set<InterviewerProfile>();
    public DbSet<InterviewerPriority> InterviewerPriorities => Set<InterviewerPriority>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<InterviewerExperience> InterviewerExperiences => Set<InterviewerExperience>();
    public DbSet<InterviewerExperienceQuestion> InterviewerExperienceQuestions => Set<InterviewerExperienceQuestion>();
    public DbSet<ModerationLog> ModerationLogs => Set<ModerationLog>();
    public DbSet<InterviewStage> InterviewStages => Set<InterviewStage>();
    public DbSet<InterviewStageQuestion> InterviewStageQuestions => Set<InterviewStageQuestion>();
    public DbSet<QuestionDailySummary> QuestionDailySummaries => Set<QuestionDailySummary>();
    public DbSet<CompanyDailySummary> CompanyDailySummaries => Set<CompanyDailySummary>();
    public DbSet<InterviewerPrioritySummary> InterviewerPrioritySummaries => Set<InterviewerPrioritySummary>();
    public DbSet<ReportRefreshState> ReportRefreshStates => Set<ReportRefreshState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(x => x.YearsOfExperience);
            entity.ToTable(t => t.HasCheckConstraint("CK_Users_YearsOfExperience", "[YearsOfExperience] IS NULL OR [YearsOfExperience] >= 0"));
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ReferenceCollection).HasMaxLength(80);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.ReferenceCollection);
            // Preserve an alias after manual merge so old question IDs remain resolvable.
            entity.HasOne(x => x.MergedIntoQuestion).WithMany().HasForeignKey(x => x.MergedIntoQuestionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.MergedIntoQuestionId);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<QuestionTag>(entity =>
        {
            entity.HasKey(x => new { x.QuestionId, x.TagId });
            entity.HasOne(x => x.Question).WithMany(x => x.QuestionTags).HasForeignKey(x => x.QuestionId);
            entity.HasOne(x => x.Tag).WithMany(x => x.QuestionTags).HasForeignKey(x => x.TagId);
            entity.HasIndex(x => x.TagId);
        });

        modelBuilder.Entity<InterviewExperience>(entity =>
        {
            entity.HasOne(x => x.Author).WithMany(x => x.Experiences).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Company).WithMany(x => x.Experiences).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Level).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.JobAdText).HasMaxLength(10000);
            entity.Property(x => x.JobAdUrl).HasMaxLength(2048);
            entity.Property(x => x.Description).HasMaxLength(10000);
            entity.Property(x => x.RatingReason).HasMaxLength(2000);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Experience_ConductRating", "[InterviewerConductRating] IS NULL OR [InterviewerConductRating] BETWEEN 1 AND 5");
                t.HasCheckConstraint("CK_Experience_AdRating", "[JobAdAlignmentRating] IS NULL OR [JobAdAlignmentRating] BETWEEN 1 AND 5");
            });
            entity.HasIndex(x => new { x.Status, x.InterviewDate });
            entity.HasIndex(x => new { x.CompanyId, x.Status, x.InterviewDate });
            entity.HasIndex(x => new { x.Level, x.Status, x.InterviewDate });
        });

        modelBuilder.Entity<ExperienceQuestion>(entity =>
        {
            entity.HasKey(x => new { x.InterviewExperienceId, x.QuestionId });
            entity.HasOne(x => x.InterviewExperience).WithMany(x => x.ExperienceQuestions).HasForeignKey(x => x.InterviewExperienceId);
            entity.HasOne(x => x.Question).WithMany(x => x.ExperienceQuestions).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.QuestionId);
            entity.ToTable(t => t.HasCheckConstraint("CK_ExperienceQuestion_Difficulty", "[PerceivedDifficulty] IS NULL OR [PerceivedDifficulty] BETWEEN 1 AND 5"));
        });

        modelBuilder.Entity<InterviewStage>(entity =>
        {
            entity.HasOne(x => x.InterviewExperience).WithMany(x => x.Stages).HasForeignKey(x => x.InterviewExperienceId);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Description).HasMaxLength(10000).IsRequired();
            entity.HasIndex(x => new { x.InterviewExperienceId, x.Order }).IsUnique();
        });

        modelBuilder.Entity<InterviewStageQuestion>(entity =>
        {
            entity.HasOne(x => x.Stage).WithMany(x => x.Questions).HasForeignKey(x => x.InterviewStageId);
            entity.HasOne(x => x.Question).WithMany(x => x.StageQuestions).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.InterviewStageId, x.Order }).IsUnique();
            entity.HasIndex(x => x.QuestionId);
            entity.ToTable(t => t.HasCheckConstraint("CK_InterviewStageQuestion_Difficulty", "[PerceivedDifficulty] IS NULL OR [PerceivedDifficulty] BETWEEN 1 AND 5"));
        });

        modelBuilder.Entity<InterviewerProfile>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.HasOne(x => x.User).WithOne(x => x.InterviewerProfile).HasForeignKey<InterviewerProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Bio).HasMaxLength(2000);
            entity.Property(x => x.AdviceText).HasMaxLength(5000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.UpdatedAt });
        });

        modelBuilder.Entity<InterviewerPriority>(entity =>
        {
            entity.HasOne(x => x.InterviewerProfile).WithMany(x => x.Priorities).HasForeignKey(x => x.InterviewerUserId);
            entity.Property(x => x.Skill).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => new { x.InterviewerUserId, x.Skill }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_InterviewerPriority_Importance", "[Importance] BETWEEN 1 AND 5"));
        });

        modelBuilder.Entity<ContentReport>(entity =>
        {
            entity.HasOne(x => x.InterviewExperience).WithMany(x => x.Reports).HasForeignKey(x => x.InterviewExperienceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.InterviewExperienceId, x.ReporterId }).IsUnique();
        });

        // Public projections must check both the moderation status and each visibility flag.
        modelBuilder.Entity<InterviewerExperience>(entity =>
        {
            entity.HasOne(x => x.Author).WithMany(x => x.InterviewerExperiences).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Company).WithMany(x => x.InterviewerExperiences).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.InterviewerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(10000).IsRequired();
            entity.Property(x => x.JobAdText).HasMaxLength(10000);
            entity.Property(x => x.JobAdUrl).HasMaxLength(2048);
            entity.Property(x => x.Level).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.ToTable(t => t.HasCheckConstraint("CK_InterviewerExperience_Rating", "[InterviewRating] BETWEEN 1 AND 5"));
            entity.HasIndex(x => new { x.Status, x.InterviewDate });
            entity.HasIndex(x => new { x.AuthorId, x.CreatedAt });
            entity.HasIndex(x => new { x.CompanyId, x.Status, x.InterviewDate });
        });

        modelBuilder.Entity<InterviewerExperienceQuestion>(entity =>
        {
            entity.HasOne(x => x.InterviewerExperience).WithMany(x => x.Questions).HasForeignKey(x => x.InterviewerExperienceId);
            entity.HasOne(x => x.Question).WithMany(x => x.InterviewerQuestions).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.QuestionId);
        });

        modelBuilder.Entity<ModerationLog>(entity =>
        {
            entity.HasOne(x => x.Admin).WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.ContentType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Action).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.ContentType, x.ContentId, x.CreatedAt });
        });

        modelBuilder.Entity<QuestionDailySummary>(entity =>
        {
            entity.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Level).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.Day, x.CompanyId, x.Level, x.QuestionId }).IsUnique();
            entity.HasIndex(x => x.QuestionId);
        });

        modelBuilder.Entity<CompanyDailySummary>(entity =>
        {
            entity.Property(x => x.Level).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.Day, x.CompanyId, x.Level }).IsUnique();
        });

        modelBuilder.Entity<InterviewerPrioritySummary>(entity =>
        {
            entity.Property(x => x.Skill).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedSkill).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.NormalizedSkill).IsUnique();
        });
    }
}
