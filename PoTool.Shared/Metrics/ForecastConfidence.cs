namespace PoTool.Shared.Metrics;

/// <summary>
/// Confidence level for forecast predictions.
/// </summary>
public enum ForecastConfidence
{
    Low = 0,      // Less than 3 sprints of historical data
    Medium = 1,   // 3-5 sprints of historical data
    High = 2      // 5+ sprints of historical data with stable velocity
}
