namespace PoTool.Core.Domain.Forecasting.Models;

public enum ForecastConfidenceLevel
{
    Low,
    Medium,
    High
}

public sealed record HistoricalVelocitySample
{
    public HistoricalVelocitySample(string sprintName, DateTimeOffset? sprintEndDate, double completedStoryPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sprintName);
        ForecastingModelValidation.ValidateNonNegative(completedStoryPoints, nameof(completedStoryPoints), "Completed story points");

        SprintName = sprintName;
        SprintEndDate = sprintEndDate;
        CompletedStoryPoints = completedStoryPoints;
    }

    public string SprintName { get; }

    public DateTimeOffset? SprintEndDate { get; }

    public double CompletedStoryPoints { get; }
}

public sealed record DeliveryForecast
{
    public DeliveryForecast(
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        double remainingScopeStoryPoints,
        double estimatedVelocity,
        int sprintsRemaining,
        DateTimeOffset? estimatedCompletionDate,
        ForecastConfidenceLevel confidence,
        IReadOnlyList<CompletionProjection> projections)
    {
        ForecastingModelValidation.ValidateNonNegative(totalScopeStoryPoints, nameof(totalScopeStoryPoints), "Total scope story points");
        ForecastingModelValidation.ValidateNonNegative(completedScopeStoryPoints, nameof(completedScopeStoryPoints), "Completed scope story points");
        ForecastingModelValidation.ValidateNonNegative(remainingScopeStoryPoints, nameof(remainingScopeStoryPoints), "Remaining scope story points");
        ForecastingModelValidation.ValidateNonNegative(estimatedVelocity, nameof(estimatedVelocity), "Estimated velocity");
        ForecastingModelValidation.ValidateCount(sprintsRemaining, nameof(sprintsRemaining), "Sprints remaining");
        ArgumentNullException.ThrowIfNull(projections);

        TotalScopeStoryPoints = totalScopeStoryPoints;
        CompletedScopeStoryPoints = completedScopeStoryPoints;
        RemainingScopeStoryPoints = remainingScopeStoryPoints;
        EstimatedVelocity = estimatedVelocity;
        SprintsRemaining = sprintsRemaining;
        EstimatedCompletionDate = estimatedCompletionDate;
        Confidence = confidence;
        Projections = projections;
    }

    public double TotalScopeStoryPoints { get; }

    public double CompletedScopeStoryPoints { get; }

    public double RemainingScopeStoryPoints { get; }

    public double EstimatedVelocity { get; }

    public int SprintsRemaining { get; }

    public DateTimeOffset? EstimatedCompletionDate { get; }

    public ForecastConfidenceLevel Confidence { get; }

    public IReadOnlyList<CompletionProjection> Projections { get; }
}
