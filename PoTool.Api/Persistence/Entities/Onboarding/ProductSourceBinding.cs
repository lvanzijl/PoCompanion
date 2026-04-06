namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class ProductSourceBinding : OnboardingGraphEntityBase
{
    public int ProductRootId { get; set; }

    public int ProjectSourceId { get; set; }

    public int? TeamSourceId { get; set; }

    public int? PipelineSourceId { get; set; }

    public ProductSourceType SourceType { get; set; }

    public string SourceExternalId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public OnboardingValidationState ValidationState { get; set; } = new();

    public ProductRoot ProductRoot { get; set; } = null!;

    public ProjectSource ProjectSource { get; set; } = null!;

    public TeamSource? TeamSource { get; set; }

    public PipelineSource? PipelineSource { get; set; }
}
