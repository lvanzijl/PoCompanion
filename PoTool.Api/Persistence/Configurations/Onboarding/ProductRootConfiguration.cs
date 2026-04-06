using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class ProductRootConfiguration : IEntityTypeConfiguration<ProductRoot>
{
    public void Configure(EntityTypeBuilder<ProductRoot> builder)
    {
        builder.ToTable("OnboardingProductRoots");

        builder.HasKey(root => root.Id);

        OnboardingEntityConfigurationHelpers.ConfigureEntityBase(builder);

        builder.Property(root => root.WorkItemExternalId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(root => root.Enabled)
            .IsRequired();

        builder.HasIndex(root => new { root.ProjectSourceId, root.WorkItemExternalId })
            .IsUnique();

        builder.HasOne(root => root.ProjectSource)
            .WithMany(project => project.ProductRoots)
            .HasForeignKey(root => root.ProjectSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(root => root.Snapshot, snapshot =>
        {
            snapshot.Property(value => value.WorkItemExternalId)
                .HasMaxLength(64)
                .IsRequired();

            snapshot.Property(value => value.Title)
                .HasMaxLength(512)
                .IsRequired();

            snapshot.Property(value => value.WorkItemType)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.State)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.ProjectExternalId)
                .HasMaxLength(128)
                .IsRequired();

            snapshot.Property(value => value.AreaPath)
                .HasMaxLength(512)
                .IsRequired();

            snapshot.OwnsOne(value => value.Metadata, metadata =>
            {
                OnboardingEntityConfigurationHelpers.ConfigureSnapshotMetadata(metadata);
            });

            snapshot.Navigation(value => value.Metadata)
                .IsRequired();
        });

        builder.Navigation(root => root.Snapshot)
            .IsRequired();

        builder.OwnsOne(root => root.ValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(root => root.ValidationState)
            .IsRequired();
    }
}
