using PoTool.Core.Domain.Forecasting.Models;

namespace PoTool.Core.Domain.Forecasting.Components.DeliveryForecast;

public sealed record ForecastProjection
{
    public ForecastProjection(
        int workItemId,
        string workItemType,
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        double remainingScopeStoryPoints,
        double estimatedVelocity,
        int sprintsRemaining,
        DateTimeOffset? estimatedCompletionDate,
        ForecastConfidenceLevel confidence,
        DateTimeOffset lastUpdated,
        IReadOnlyList<CompletionProjection> forecastByDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(workItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItemType);
        ForecastingModelValidation.ValidateNonNegative(totalScopeStoryPoints, nameof(totalScopeStoryPoints), "Total scope story points");
        ForecastingModelValidation.ValidateNonNegative(completedScopeStoryPoints, nameof(completedScopeStoryPoints), "Completed scope story points");
        ForecastingModelValidation.ValidateNonNegative(remainingScopeStoryPoints, nameof(remainingScopeStoryPoints), "Remaining scope story points");
        ForecastingModelValidation.ValidateNonNegative(estimatedVelocity, nameof(estimatedVelocity), "Estimated velocity");
        ForecastingModelValidation.ValidateCount(sprintsRemaining, nameof(sprintsRemaining), "Sprints remaining");
        ArgumentNullException.ThrowIfNull(forecastByDate);

        WorkItemId = workItemId;
        WorkItemType = workItemType;
        TotalScopeStoryPoints = totalScopeStoryPoints;
        CompletedScopeStoryPoints = completedScopeStoryPoints;
        RemainingScopeStoryPoints = remainingScopeStoryPoints;
        EstimatedVelocity = estimatedVelocity;
        SprintsRemaining = sprintsRemaining;
        EstimatedCompletionDate = estimatedCompletionDate;
        Confidence = confidence;
        LastUpdated = lastUpdated;
        ForecastByDate = forecastByDate;
    }

    public int WorkItemId { get; }

    public string WorkItemType { get; }

    public double TotalScopeStoryPoints { get; }

    public double CompletedScopeStoryPoints { get; }

    public double RemainingScopeStoryPoints { get; }

    public double EstimatedVelocity { get; }

    public int SprintsRemaining { get; }

    public DateTimeOffset? EstimatedCompletionDate { get; }

    public ForecastConfidenceLevel Confidence { get; }

    public DateTimeOffset LastUpdated { get; }

    public IReadOnlyList<CompletionProjection> ForecastByDate { get; }
}
