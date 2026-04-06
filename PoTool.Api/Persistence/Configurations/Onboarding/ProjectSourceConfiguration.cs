using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class ProjectSourceConfiguration : IEntityTypeConfiguration<ProjectSource>
{
    public void Configure(EntityTypeBuilder<ProjectSource> builder)
    {
        builder.ToTable("OnboardingProjectSources");

        builder.HasKey(project => project.Id);

        OnboardingEntityConfigurationHelpers.ConfigureEntityBase(builder);

        builder.Property(project => project.ProjectExternalId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(project => project.Enabled)
            .IsRequired();

        builder.HasIndex(project => new { project.TfsConnectionId, project.ProjectExternalId })
            .IsUnique();

        builder.HasOne(project => project.TfsConnection)
            .WithMany(connection => connection.ProjectSources)
            .HasForeignKey(project => project.TfsConnectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(project => project.Snapshot, snapshot =>
        {
            snapshot.Property(value => value.ProjectExternalId)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.Name)
                .HasMaxLength(256)
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

        builder.Navigation(project => project.Snapshot)
            .IsRequired();

        builder.OwnsOne(project => project.ValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(project => project.ValidationState)
            .IsRequired();
    }
}
