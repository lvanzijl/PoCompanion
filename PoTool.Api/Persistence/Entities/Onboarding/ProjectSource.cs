namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class ProjectSource : OnboardingEntityBase
{
    public int TfsConnectionId { get; set; }

    public string ProjectExternalId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public ProjectSnapshot Snapshot { get; set; } = new();

    public OnboardingValidationState ValidationState { get; set; } = new();

    public TfsConnection TfsConnection { get; set; } = null!;

    public ICollection<TeamSource> TeamSources { get; set; } = new List<TeamSource>();

    public ICollection<PipelineSource> PipelineSources { get; set; } = new List<PipelineSource>();

    public ICollection<ProductRoot> ProductRoots { get; set; } = new List<ProductRoot>();

    public ICollection<ProductSourceBinding> ProductSourceBindings { get; set; } = new List<ProductSourceBinding>();
}
