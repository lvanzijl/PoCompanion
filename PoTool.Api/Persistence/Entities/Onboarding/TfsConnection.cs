namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class TfsConnection : OnboardingEntityBase
{
    public TfsConnection()
    {
        LastAttemptedValidationAtUtc = DateTime.UtcNow;
    }

    public string ConnectionKey { get; set; } = "connection";

    public string OrganizationUrl { get; set; } = string.Empty;

    public string AuthenticationMode { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; }

    public string ApiVersion { get; set; } = string.Empty;

    public OnboardingValidationState AvailabilityValidationState { get; set; } = new();

    public OnboardingValidationState PermissionValidationState { get; set; } = new();

    public OnboardingValidationState CapabilityValidationState { get; set; } = new();

    public DateTime? LastSuccessfulValidationAtUtc { get; set; }

    public DateTime LastAttemptedValidationAtUtc { get; set; }

    public string? ValidationFailureReason { get; set; }

    public string? LastVerifiedCapabilitiesSummary { get; set; }

    public ICollection<ProjectSource> ProjectSources { get; set; } = new List<ProjectSource>();
}
