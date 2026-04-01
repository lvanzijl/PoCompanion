using PoTool.Core.Domain.Forecasting.Components.DeliveryForecast;
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
    private readonly IDeliveryForecastProjector _projector;

    public CompletionForecastService()
        : this(new DeliveryForecastProjector())
    {
    }

    public CompletionForecastService(IDeliveryForecastProjector projector)
    {
        _projector = projector;
    }

    public DeliveryForecast Forecast(
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        IReadOnlyList<HistoricalVelocitySample> historicalSprints)
    {
        var projection = _projector.Project(
            workItemId: 0,
            workItemType: "Forecast",
            totalScopeStoryPoints,
            completedScopeStoryPoints,
            historicalSprints,
            DateTimeOffset.UtcNow);

        return new DeliveryForecast(
            projection.TotalScopeStoryPoints,
            projection.CompletedScopeStoryPoints,
            projection.RemainingScopeStoryPoints,
            projection.EstimatedVelocity,
            projection.SprintsRemaining,
            projection.EstimatedCompletionDate,
            projection.Confidence,
            projection.ForecastByDate);
    }
}
