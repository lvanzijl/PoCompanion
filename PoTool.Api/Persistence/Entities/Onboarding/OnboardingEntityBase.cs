namespace PoTool.Api.Persistence.Entities.Onboarding;

public abstract class OnboardingEntityBase
{
    protected OnboardingEntityBase()
    {
        var utcNow = DateTime.UtcNow;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
