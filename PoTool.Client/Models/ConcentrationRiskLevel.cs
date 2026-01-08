namespace PoTool.Client.Models;

/// <summary>
/// Risk level for effort concentration.
/// Client-side copy that matches PoTool.Core.Metrics.ConcentrationRiskLevel.
/// </summary>
public enum ConcentrationRiskLevel
{
    None = 0,     // < 25% concentration
    Low = 1,      // 25-40% concentration
    Medium = 2,   // 40-60% concentration
    High = 3,     // 60-80% concentration
    Critical = 4  // > 80% concentration
}
