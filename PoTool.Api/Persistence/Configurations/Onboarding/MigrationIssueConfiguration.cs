using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class MigrationIssueConfiguration : IEntityTypeConfiguration<MigrationIssue>
{
    public void Configure(EntityTypeBuilder<MigrationIssue> builder)
    {
        builder.ToTable("OnboardingMigrationIssues");

        builder.HasKey(issue => issue.Id);

        builder.Property(issue => issue.IssueIdentifier)
            .IsRequired();

        builder.Property(issue => issue.IssueType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(issue => issue.IssueCategory)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(issue => issue.Severity)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(issue => issue.SourceLegacyReference)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(issue => issue.TargetEntityType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(issue => issue.TargetExternalIdentity)
            .HasMaxLength(256);

        builder.Property(issue => issue.SanitizedMessage)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(issue => issue.SanitizedDetails)
            .HasMaxLength(4096);

        builder.Property(issue => issue.CreatedAtUtc)
            .IsRequired();

        builder.Property(issue => issue.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(issue => issue.IssueIdentifier)
            .IsUnique();

        builder.HasIndex(issue => new { issue.MigrationRunId, issue.Severity, issue.IssueCategory });

        builder.HasOne(issue => issue.MigrationRun)
            .WithMany(run => run.Issues)
            .HasForeignKey(issue => issue.MigrationRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(issue => issue.MigrationUnit)
            .WithMany(unit => unit.Issues)
            .HasForeignKey(issue => issue.MigrationUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
