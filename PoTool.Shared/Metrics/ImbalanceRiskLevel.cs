namespace PoTool.Shared.Metrics;

/// <summary>
/// Risk level for effort imbalance.
/// </summary>
public enum ImbalanceRiskLevel
{
    Low = 0,      // Deviation < 30%
    Medium = 1,   // Deviation 30-50%
    High = 2,     // Deviation 50-80%
    Critical = 3  // Deviation > 80%
}
