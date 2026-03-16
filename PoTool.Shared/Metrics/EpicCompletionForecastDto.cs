namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing completion forecast for an Epic or Feature.
/// Uses historical velocity to predict completion date.
/// The scope fields retain their legacy names for API compatibility,
/// but now represent canonical story-point rollups and may be fractional
/// when derived estimates are used.
/// </summary>
/// <param name="EpicId">TFS ID of the requested epic or feature.</param>
/// <param name="Title">Display title of the requested work item.</param>
/// <param name="Type">Work item type of the requested item.</param>
/// <param name="TotalEffort">Legacy contract name for total canonical story-point scope. Compatibility alias; deprecated in future contract revision.</param>
/// <param name="CompletedEffort">Legacy contract name for completed canonical story-point scope. Compatibility alias; deprecated in future contract revision.</param>
/// <param name="RemainingEffort">Legacy contract name for remaining canonical story-point scope. Compatibility alias; deprecated in future contract revision.</param>
/// <param name="EstimatedVelocity">Average delivered story points across the sampled historical sprints.</param>
/// <param name="SprintsRemaining">Forecasted sprint count required to finish the remaining scope.</param>
/// <param name="EstimatedCompletionDate">Projected completion date using the estimated velocity.</param>
/// <param name="Confidence">Confidence derived from the amount of sprint history available.</param>
/// <param name="ForecastByDate">Sprint-by-sprint projection of completed and remaining scope.</param>
/// <param name="AreaPath">Area path used to scope the forecast history.</param>
/// <param name="AnalysisTimestamp">UTC timestamp when the forecast was computed.</param>
public sealed record EpicCompletionForecastDto(
    int EpicId,
    string Title,
    string Type,
    double TotalEffort,
    double CompletedEffort,
    double RemainingEffort,
    double EstimatedVelocity,
    int SprintsRemaining,
    DateTimeOffset? EstimatedCompletionDate,
    ForecastConfidence Confidence,
    IReadOnlyList<SprintForecast> ForecastByDate,
    string AreaPath,
    DateTimeOffset AnalysisTimestamp
)
{
    /// <summary>
    /// Canonical alias for completed canonical story-point scope.
    /// Maps to the same internal value as CompletedEffort.
    /// </summary>
    public double DeliveredStoryPoints => CompletedEffort;

    /// <summary>
    /// Canonical alias for remaining canonical story-point scope.
    /// Maps to the same internal value as RemainingEffort.
    /// </summary>
    public double RemainingStoryPoints => RemainingEffort;
}

/// <summary>
/// Sprint-by-sprint forecast showing predicted progress.
/// </summary>
/// <param name="SprintName">Display name for the forecast sprint bucket.</param>
/// <param name="IterationPath">Synthetic iteration path used for the forecast bucket.</param>
/// <param name="SprintStartDate">Forecast sprint start date.</param>
/// <param name="SprintEndDate">Forecast sprint end date.</param>
/// <param name="ExpectedCompletedEffort">Legacy contract name for expected completed scope in story points. Compatibility alias; deprecated in future contract revision.</param>
/// <param name="RemainingEffortAfterSprint">Legacy contract name for remaining scope after the sprint, in story points. Compatibility alias; deprecated in future contract revision.</param>
/// <param name="ProgressPercentage">Cumulative completion percentage after this forecast sprint.</param>
public sealed record SprintForecast(
    string SprintName,
    string IterationPath,
    DateTimeOffset SprintStartDate,
    DateTimeOffset SprintEndDate,
    double ExpectedCompletedEffort,
    double RemainingEffortAfterSprint,
    double ProgressPercentage
);

/// <summary>
/// Confidence level for forecast prediction.
/// </summary>
public enum ForecastConfidence
{
    Low,      // Less than 3 sprints of historical data
    Medium,   // 3-5 sprints of historical data
    High      // 5+ sprints of historical data with stable velocity
}
