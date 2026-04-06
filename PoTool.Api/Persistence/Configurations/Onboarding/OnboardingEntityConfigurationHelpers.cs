using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal static class OnboardingEntityConfigurationHelpers
{
    internal static void ConfigureEntityBase<T>(EntityTypeBuilder<T> builder)
        where T : OnboardingGraphEntityBase
    {
        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.DeletedAtUtc);

        builder.Property(entity => entity.DeletionReason)
            .HasMaxLength(2048);

        builder.Property(entity => entity.IsDeleted)
            .IsRequired();

        builder.HasIndex(entity => entity.IsDeleted);
    }

    internal static void ConfigureAuditFields<T>(OwnedNavigationBuilder<T, OnboardingValidationState> builder)
        where T : class
    {
        builder.Property(state => state.Status)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(state => state.ValidatedAtUtc)
            .IsRequired();

        builder.Property(state => state.ErrorCode)
            .HasMaxLength(128);

        builder.Property(state => state.Message)
            .HasMaxLength(2048);

        builder.Property(state => state.IsRetryable)
            .IsRequired();
    }

    internal static void ConfigureSnapshotMetadata<T>(
        OwnedNavigationBuilder<T, OnboardingSnapshotMetadata> builder)
        where T : class
    {
        builder.Property(metadata => metadata.ConfirmedAtUtc)
            .IsRequired();

        builder.Property(metadata => metadata.LastSeenAtUtc)
            .IsRequired();

        builder.Property(metadata => metadata.IsCurrent)
            .IsRequired();

        builder.Property(metadata => metadata.RenameDetected)
            .IsRequired();

        builder.Property(metadata => metadata.StaleReason)
            .HasMaxLength(512);
    }
}
