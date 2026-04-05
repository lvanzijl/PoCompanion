namespace PoTool.Api.Persistence.Entities.Onboarding;

public abstract class OnboardingEntityBase
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
