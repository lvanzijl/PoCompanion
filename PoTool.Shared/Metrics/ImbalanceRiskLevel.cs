namespace PoTool.Shared.Metrics;

/// <summary>
/// Risk level for effort imbalance.
/// </summary>
public enum ImbalanceRiskLevel
{
    None = 0,
    Low = 1,      // Deviation < 30%
    Medium = 2,   // Deviation 30-50%
    High = 3      // Deviation 50-80%
}
