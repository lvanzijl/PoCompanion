using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class ProductSourceBindingConfiguration : IEntityTypeConfiguration<ProductSourceBinding>
{
    public void Configure(EntityTypeBuilder<ProductSourceBinding> builder)
    {
        builder.ToTable(
            "OnboardingProductSourceBindings",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_OnboardingProductSourceBindings_SourceReference",
                """
                ("SourceType" = 'Project' AND "TeamSourceId" IS NULL AND "PipelineSourceId" IS NULL) OR
                ("SourceType" = 'Team' AND "TeamSourceId" IS NOT NULL AND "PipelineSourceId" IS NULL) OR
                ("SourceType" = 'Pipeline' AND "PipelineSourceId" IS NOT NULL AND "TeamSourceId" IS NULL)
                """));

        builder.HasKey(binding => binding.Id);

        builder.Property(binding => binding.SourceType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(binding => binding.SourceExternalId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(binding => binding.Enabled)
            .IsRequired();

        builder.Property(binding => binding.CreatedAtUtc)
            .IsRequired();

        builder.Property(binding => binding.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(binding => new
        {
            binding.ProductRootId,
            binding.SourceType,
            binding.SourceExternalId
        }).IsUnique();

        builder.HasIndex(binding => binding.ProjectSourceId);

        builder.HasIndex(binding => binding.TeamSourceId);

        builder.HasIndex(binding => binding.PipelineSourceId);

        builder.HasOne(binding => binding.ProductRoot)
            .WithMany(root => root.ProductSourceBindings)
            .HasForeignKey(binding => binding.ProductRootId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(binding => binding.ProjectSource)
            .WithMany(project => project.ProductSourceBindings)
            .HasForeignKey(binding => binding.ProjectSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(binding => binding.TeamSource)
            .WithMany(team => team.ProductSourceBindings)
            .HasForeignKey(binding => binding.TeamSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(binding => binding.PipelineSource)
            .WithMany(pipeline => pipeline.ProductSourceBindings)
            .HasForeignKey(binding => binding.PipelineSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(binding => binding.ValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(binding => binding.ValidationState)
            .IsRequired();
    }
}
