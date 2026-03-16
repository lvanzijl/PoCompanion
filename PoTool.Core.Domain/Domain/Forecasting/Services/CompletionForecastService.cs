using PoTool.Core.Domain.Forecasting.Models;

namespace PoTool.Core.Domain.Forecasting.Services;

public interface ICompletionForecastService
{
    DeliveryForecast Forecast(
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        IReadOnlyList<HistoricalVelocitySample> historicalSprints);
}

public sealed class CompletionForecastService : ICompletionForecastService
{
    private const int SprintCadenceDays = 14;
    private const int MaxProjectedSprints = 20;

    public DeliveryForecast Forecast(
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        IReadOnlyList<HistoricalVelocitySample> historicalSprints)
    {
        ArgumentNullException.ThrowIfNull(historicalSprints);

        var remainingScopeStoryPoints = totalScopeStoryPoints - completedScopeStoryPoints;
        var estimatedVelocity = historicalSprints.Count > 0
            ? historicalSprints.Average(sprint => sprint.CompletedStoryPoints)
            : 0d;
        var sprintsRemaining = estimatedVelocity > 0
            ? (int)Math.Ceiling(remainingScopeStoryPoints / estimatedVelocity)
            : 0;

        return new DeliveryForecast(
            totalScopeStoryPoints,
            completedScopeStoryPoints,
            remainingScopeStoryPoints,
            estimatedVelocity,
            sprintsRemaining,
            BuildEstimatedCompletionDate(historicalSprints, sprintsRemaining),
            DetermineConfidence(historicalSprints.Count),
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
