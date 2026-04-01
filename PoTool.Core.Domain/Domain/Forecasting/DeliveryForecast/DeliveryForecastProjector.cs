using PoTool.Core.Domain.Forecasting.Models;

namespace PoTool.Core.Domain.Forecasting.Components.DeliveryForecast;

public interface IDeliveryForecastProjector
{
    ForecastProjection Project(
        int workItemId,
        string workItemType,
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        IReadOnlyList<HistoricalVelocitySample> historicalSprints,
        DateTimeOffset lastUpdated);
}

public sealed class DeliveryForecastProjector : IDeliveryForecastProjector
{
    private const int SprintCadenceDays = 14;
    private const int MaxProjectedSprints = 20;

    public ForecastProjection Project(
        int workItemId,
        string workItemType,
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        IReadOnlyList<HistoricalVelocitySample> historicalSprints,
        DateTimeOffset lastUpdated)
    {
        ArgumentNullException.ThrowIfNull(historicalSprints);

        var remainingScopeStoryPoints = totalScopeStoryPoints - completedScopeStoryPoints;
        var estimatedVelocity = historicalSprints.Count > 0
            ? historicalSprints.Average(sprint => sprint.CompletedStoryPoints)
            : 0d;
        var sprintsRemaining = estimatedVelocity > 0
            ? (int)Math.Ceiling(remainingScopeStoryPoints / estimatedVelocity)
            : 0;

        return new ForecastProjection(
            workItemId,
            workItemType,
            totalScopeStoryPoints,
            completedScopeStoryPoints,
            remainingScopeStoryPoints,
            estimatedVelocity,
            sprintsRemaining,
            BuildEstimatedCompletionDate(historicalSprints, sprintsRemaining),
            DetermineConfidence(historicalSprints.Count),
            lastUpdated,
            BuildProjections(historicalSprints, remainingScopeStoryPoints, estimatedVelocity));
    }

    private static DateTimeOffset? BuildEstimatedCompletionDate(
        IReadOnlyList<HistoricalVelocitySample> historicalSprints,
        int sprintsRemaining)
    {
        if (historicalSprints.Count == 0 || sprintsRemaining <= 0)
        {
            return null;
        }

        var lastSprint = historicalSprints
            .Where(static sprint => sprint.SprintEndDate.HasValue)
            .OrderBy(static sprint => sprint.SprintEndDate)
            .LastOrDefault();

        return lastSprint?.SprintEndDate?.AddDays(sprintsRemaining * SprintCadenceDays);
    }

    private static ForecastConfidenceLevel DetermineConfidence(int sprintCount)
    {
        if (sprintCount < 3)
        {
            return ForecastConfidenceLevel.Low;
        }

        if (sprintCount < 5)
        {
            return ForecastConfidenceLevel.Medium;
        }

        return ForecastConfidenceLevel.High;
    }

    private static IReadOnlyList<CompletionProjection> BuildProjections(
        IReadOnlyList<HistoricalVelocitySample> historicalSprints,
        double remainingScopeStoryPoints,
        double estimatedVelocity)
    {
        if (historicalSprints.Count == 0 || estimatedVelocity <= 0)
        {
            return Array.Empty<CompletionProjection>();
        }

        var lastSprint = historicalSprints
            .Where(static sprint => sprint.SprintEndDate.HasValue)
            .OrderBy(static sprint => sprint.SprintEndDate)
            .LastOrDefault();

        if (lastSprint?.SprintEndDate is null)
        {
            return Array.Empty<CompletionProjection>();
        }

        var projections = new List<CompletionProjection>();
        var currentRemaining = remainingScopeStoryPoints;
        var sprintNumber = 1;

        while (currentRemaining > 0 && sprintNumber <= MaxProjectedSprints)
        {
            var sprintStart = lastSprint.SprintEndDate.Value.AddDays((sprintNumber - 1) * SprintCadenceDays);
            var sprintEnd = sprintStart.AddDays(SprintCadenceDays);
            var expectedCompleted = Math.Min(currentRemaining, estimatedVelocity);

            currentRemaining = Math.Max(0d, currentRemaining - expectedCompleted);

            var progressPercentage = remainingScopeStoryPoints > 0
                ? ((remainingScopeStoryPoints - currentRemaining) / remainingScopeStoryPoints) * 100
                : 100;

            projections.Add(new CompletionProjection(
                $"Sprint +{sprintNumber}",
                $"Forecast/{sprintNumber}",
                sprintStart,
                sprintEnd,
                expectedCompleted,
                currentRemaining,
                progressPercentage));

            sprintNumber++;
        }

        return projections;
    }
}
