using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class TeamSourceConfiguration : IEntityTypeConfiguration<TeamSource>
{
    public void Configure(EntityTypeBuilder<TeamSource> builder)
    {
        builder.ToTable("OnboardingTeamSources");

        builder.HasKey(team => team.Id);

        OnboardingEntityConfigurationHelpers.ConfigureEntityBase(builder);

        builder.Property(team => team.TeamExternalId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(team => team.Enabled)
            .IsRequired();

        builder.HasIndex(team => new { team.ProjectSourceId, team.TeamExternalId })
            .IsUnique();

        builder.HasOne(team => team.ProjectSource)
            .WithMany(project => project.TeamSources)
            .HasForeignKey(team => team.ProjectSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(team => team.Snapshot, snapshot =>
        {
            snapshot.Property(value => value.TeamExternalId)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.ProjectExternalId)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.Name)
                .HasMaxLength(256)
                .IsRequired();

            snapshot.Property(value => value.DefaultAreaPath)
                .HasMaxLength(512)
                .IsRequired();

            snapshot.Property(value => value.Description)
                .HasMaxLength(2048);

            snapshot.OwnsOne(value => value.Metadata, metadata =>
            {
                OnboardingEntityConfigurationHelpers.ConfigureSnapshotMetadata(metadata);
            });

            snapshot.Navigation(value => value.Metadata)
                .IsRequired();
        });

        builder.Navigation(team => team.Snapshot)
            .IsRequired();

        builder.OwnsOne(team => team.ValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(team => team.ValidationState)
            .IsRequired();
    }
}
