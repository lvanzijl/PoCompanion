namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class TeamSource : OnboardingGraphEntityBase
{
    public int ProjectSourceId { get; set; }

    public string TeamExternalId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public TeamSnapshot Snapshot { get; set; } = new();

    public OnboardingValidationState ValidationState { get; set; } = new();

    public ProjectSource ProjectSource { get; set; } = null!;

    public ICollection<ProductSourceBinding> ProductSourceBindings { get; set; } = new List<ProductSourceBinding>();
}
