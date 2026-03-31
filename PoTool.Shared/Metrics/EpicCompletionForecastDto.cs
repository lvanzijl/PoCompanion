namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing completion forecast for an Epic or Feature.
/// Uses historical velocity to predict completion date.
/// Scope and forecast fields use canonical story-point naming and may be
/// fractional when derived estimates are used.
/// </summary>
/// <param name="EpicId">TFS ID of the requested epic or feature.</param>
/// <param name="Title">Display title of the requested work item.</param>
/// <param name="Type">Work item type of the requested item.</param>
/// <param name="TotalStoryPoints">Total canonical story-point scope.</param>
/// <param name="DoneStoryPoints">Completed canonical story-point scope.</param>
/// <param name="RemainingStoryPoints">Remaining canonical story-point scope.</param>
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
    double TotalStoryPoints,
    double DoneStoryPoints,
    double RemainingStoryPoints,
    double EstimatedVelocity,
    int SprintsRemaining,
    DateTimeOffset? EstimatedCompletionDate,
    ForecastConfidence Confidence,
    IReadOnlyList<SprintForecast> ForecastByDate,
    string AreaPath,
    DateTimeOffset AnalysisTimestamp
)
{
    public EpicCompletionForecastDto()
        : this(0, string.Empty, string.Empty, 0, 0, 0, 0, 0, null, ForecastConfidence.Low, Array.Empty<SprintForecast>(), string.Empty, default)
    {
    }

    /// <summary>
    /// Canonical alias for completed canonical story-point scope.
    /// </summary>
    public double DeliveredStoryPoints => DoneStoryPoints;
}

/// <summary>
/// Sprint-by-sprint forecast showing predicted progress.
/// </summary>
/// <param name="SprintName">Display name for the forecast sprint bucket.</param>
/// <param name="IterationPath">Synthetic iteration path used for the forecast bucket.</param>
/// <param name="SprintStartDate">Forecast sprint start date.</param>
/// <param name="SprintEndDate">Forecast sprint end date.</param>
/// <param name="ExpectedCompletedStoryPoints">Expected completed scope in story points after this forecast sprint.</param>
/// <param name="RemainingStoryPointsAfterSprint">Remaining scope after this forecast sprint, in story points.</param>
/// <param name="ProgressPercentage">Cumulative completion percentage after this forecast sprint.</param>
public sealed record SprintForecast(
    string SprintName,
    string IterationPath,
    DateTimeOffset SprintStartDate,
    DateTimeOffset SprintEndDate,
    double ExpectedCompletedStoryPoints,
    double RemainingStoryPointsAfterSprint,
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
