using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Persistence.Configurations.Onboarding;

internal sealed class TfsConnectionConfiguration : IEntityTypeConfiguration<TfsConnection>
{
    public void Configure(EntityTypeBuilder<TfsConnection> builder)
    {
        builder.ToTable("OnboardingTfsConnections");

        builder.HasKey(connection => connection.Id);

        OnboardingEntityConfigurationHelpers.ConfigureEntityBase(builder);

        builder.Property(connection => connection.ConnectionKey)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(connection => connection.OrganizationUrl)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(connection => connection.AuthenticationMode)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(connection => connection.TimeoutSeconds)
            .IsRequired();

        builder.Property(connection => connection.ApiVersion)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(connection => connection.LastAttemptedValidationAtUtc)
            .IsRequired();

        builder.Property(connection => connection.ValidationFailureReason)
            .HasMaxLength(2048);

        builder.Property(connection => connection.LastVerifiedCapabilitiesSummary)
            .HasMaxLength(2048);

        builder.HasIndex(connection => connection.ConnectionKey)
            .IsUnique();

        builder.HasIndex(connection => connection.OrganizationUrl);

        builder.OwnsOne(connection => connection.AvailabilityValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(connection => connection.AvailabilityValidationState)
            .IsRequired();

        builder.OwnsOne(connection => connection.PermissionValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(connection => connection.PermissionValidationState)
            .IsRequired();

        builder.OwnsOne(connection => connection.CapabilityValidationState, validation =>
        {
            OnboardingEntityConfigurationHelpers.ConfigureAuditFields(validation);
        });

        builder.Navigation(connection => connection.CapabilityValidationState)
            .IsRequired();
    }
}
