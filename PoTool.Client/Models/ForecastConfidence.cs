namespace PoTool.Client.Models;

/// <summary>
/// Confidence level for forecast predictions.
/// Client-side copy that matches PoTool.Core.Metrics.ForecastConfidence.
/// </summary>
public enum ForecastConfidence
{
    Low = 0,      // Less than 3 sprints of historical data
    Medium = 1,   // 3-5 sprints of historical data
    High = 2      // 5+ sprints of historical data with stable velocity
}
