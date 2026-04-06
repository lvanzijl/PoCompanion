using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class MigrationUnitConfiguration : IEntityTypeConfiguration<MigrationUnit>
{
    public void Configure(EntityTypeBuilder<MigrationUnit> builder)
    {
        builder.ToTable("OnboardingMigrationUnits");

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.UnitIdentifier)
            .IsRequired();

        builder.Property(unit => unit.UnitType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(unit => unit.UnitName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(unit => unit.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(unit => unit.CreatedAtUtc)
            .IsRequired();

        builder.Property(unit => unit.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(unit => unit.UnitIdentifier)
            .IsUnique();

        builder.HasIndex(unit => new { unit.MigrationRunId, unit.ExecutionOrder })
            .IsUnique();

        builder.HasOne(unit => unit.MigrationRun)
            .WithMany(run => run.Units)
            .HasForeignKey(unit => unit.MigrationRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(unit => unit.Issues)
            .WithOne(issue => issue.MigrationUnit)
            .HasForeignKey(issue => issue.MigrationUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
