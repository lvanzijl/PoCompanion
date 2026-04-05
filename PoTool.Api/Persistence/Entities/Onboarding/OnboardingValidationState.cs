namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class OnboardingValidationState
{
    public OnboardingValidationState()
    {
        ValidatedAtUtc = DateTime.UtcNow;
    }

    public string Status { get; set; } = "Unknown";

    public DateTime ValidatedAtUtc { get; set; }

    public string? ErrorCode { get; set; }

    public string? Message { get; set; }

    public bool IsRetryable { get; set; }
}
