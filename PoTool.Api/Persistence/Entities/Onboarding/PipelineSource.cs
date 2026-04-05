namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class PipelineSource : OnboardingEntityBase
{
    public int ProjectSourceId { get; set; }

    public string PipelineExternalId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public PipelineSnapshot Snapshot { get; set; } = new();

    public OnboardingValidationState ValidationState { get; set; } = new();

    public ProjectSource ProjectSource { get; set; } = null!;

    public ICollection<ProductSourceBinding> ProductSourceBindings { get; set; } = new List<ProductSourceBinding>();
}
