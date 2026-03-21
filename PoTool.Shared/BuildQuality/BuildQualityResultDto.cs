namespace PoTool.Shared.BuildQuality;

/// <summary>
/// BuildQuality metrics plus supporting evidence for a selected scope.
/// </summary>
public sealed class BuildQualityResultDto
{
    public BuildQualityMetricsDto Metrics { get; set; } = new();

    public BuildQualityEvidenceDto Evidence { get; set; } = new();
}
