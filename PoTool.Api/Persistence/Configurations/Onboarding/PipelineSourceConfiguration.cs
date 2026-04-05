using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class PipelineSourceConfiguration : IEntityTypeConfiguration<PipelineSource>
{
    public void Configure(EntityTypeBuilder<PipelineSource> builder)
    {
        builder.ToTable("OnboardingPipelineSources");

        builder.HasKey(pipeline => pipeline.Id);

        builder.Property(pipeline => pipeline.PipelineExternalId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(pipeline => pipeline.Enabled)
            .IsRequired();

        builder.Property(pipeline => pipeline.CreatedAtUtc)
            .IsRequired();

        builder.Property(pipeline => pipeline.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(pipeline => new { pipeline.ProjectSourceId, pipeline.PipelineExternalId })
            .IsUnique();

        builder.HasOne(pipeline => pipeline.ProjectSource)
            .WithMany(project => project.PipelineSources)
            .HasForeignKey(pipeline => pipeline.ProjectSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(pipeline => pipeline.Snapshot, snapshot =>
        {
            snapshot.Property(value => value.PipelineExternalId)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.ProjectExternalId)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.Name)
                .HasMaxLength(256)
                .IsRequired();

            snapshot.Property(value => value.Folder)
                .HasMaxLength(512);

            snapshot.Property(value => value.YamlPath)
                .HasMaxLength(1024);

            snapshot.Property(value => value.RepositoryExternalId)
                .HasMaxLength(128);

            snapshot.Property(value => value.RepositoryName)
                .HasMaxLength(256);

            snapshot.OwnsOne(value => value.Metadata, metadata =>
            {
                OnboardingEntityConfigurationHelpers.ConfigureSnapshotMetadata(metadata);
            });

            snapshot.Navigation(value => value.Metadata)
                .IsRequired();
        });

        builder.Navigation(pipeline => pipeline.Snapshot)
            .IsRequired();

        builder.OwnsOne(pipeline => pipeline.ValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(pipeline => pipeline.ValidationState)
            .IsRequired();
    }
}
