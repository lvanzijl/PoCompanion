namespace PoTool.Shared.BuildQuality;

/// <summary>
/// Canonical BuildQuality metric bundle.
/// </summary>
public sealed class BuildQualityMetricsDto
{
    public double? SuccessRate { get; set; }

    public double? TestPassRate { get; set; }

    public int TestVolume { get; set; }

    public double? Coverage { get; set; }

    public int Confidence { get; set; }
}
