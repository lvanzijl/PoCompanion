using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class MigrationRunConfiguration : IEntityTypeConfiguration<MigrationRun>
{
    public void Configure(EntityTypeBuilder<MigrationRun> builder)
    {
        builder.ToTable("OnboardingMigrationRuns");

        builder.HasKey(run => run.Id);

        builder.Property(run => run.RunIdentifier)
            .IsRequired();

        builder.Property(run => run.MigrationVersion)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(run => run.EnvironmentRing)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(run => run.TriggerType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(run => run.ExecutionMode)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(run => run.SourceFingerprint)
            .HasMaxLength(256);

        builder.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(run => run.CreatedAtUtc)
            .IsRequired();

        builder.Property(run => run.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(run => run.RunIdentifier)
            .IsUnique();

        builder.HasIndex(run => new { run.MigrationVersion, run.EnvironmentRing, run.ExecutionMode });

        builder.HasMany(run => run.Units)
            .WithOne(unit => unit.MigrationRun)
            .HasForeignKey(unit => unit.MigrationRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(run => run.Issues)
            .WithOne(issue => issue.MigrationRun)
            .HasForeignKey(issue => issue.MigrationRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
