using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Adapters;

internal static class DeliveryTrendAnalyticsExposureMapper
{
    public static ProductAggregationEpicInput ToProductAggregationEpicInput(this EpicProgressDto epicProgress)
    {
        ArgumentNullException.ThrowIfNull(epicProgress);

        return new ProductAggregationEpicInput(
            epicProgress.AggregatedProgress,
            epicProgress.ForecastConsumedEffort,
            epicProgress.ForecastRemainingEffort,
            epicProgress.TotalWeight,
            IsExcluded: false);
    }

    public static ProductProgressSummaryDto ToProductProgressSummaryDto(this ProductAggregationResult product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductProgressSummaryDto
        {
            ProductProgress = product.ProductProgress,
            ProductForecastConsumed = product.ProductForecastConsumed,
            ProductForecastRemaining = product.ProductForecastRemaining,
            ExcludedEpicsCount = product.ExcludedEpicsCount,
            IncludedEpicsCount = product.IncludedEpicsCount,
            TotalWeight = product.TotalWeight
        };
    }

    public static ProductSnapshot ToProductSnapshot(this ProductAggregationResult product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductSnapshot(
            product.ProductProgress,
            product.ProductForecastConsumed,
            product.ProductForecastRemaining);
    }

    public static SnapshotComparisonDto ToSnapshotComparisonDto(this SnapshotComparisonResult comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        return new SnapshotComparisonDto
        {
            ProgressDelta = comparison.ProgressDelta,
            ForecastConsumedDelta = comparison.ForecastConsumedDelta,
            ForecastRemainingDelta = comparison.ForecastRemainingDelta
        };
    }

    public static PlanningQualityDto ToPlanningQualityDto(this PlanningQualityResult planningQuality)
    {
        ArgumentNullException.ThrowIfNull(planningQuality);

        return new PlanningQualityDto
        {
            PlanningQualityScore = planningQuality.Score,
            PlanningQualitySignals = planningQuality.Signals
                .Select(signal => signal.ToPlanningQualitySignalDto())
                .ToList()
        };
    }

    public static PlanningQualitySignalDto ToPlanningQualitySignalDto(this PlanningQualitySignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        return new PlanningQualitySignalDto
        {
            Code = signal.Code,
            Severity = signal.Severity.ToString(),
            Scope = signal.Scope.ToString(),
            Message = signal.Message,
            EntityId = signal.EntityId
        };
    }

    public static IReadOnlyList<InsightDto> ToInsightDtos(this InsightResult insightResult)
    {
        ArgumentNullException.ThrowIfNull(insightResult);

        return insightResult.Insights
            .Select(insight => insight.ToInsightDto())
            .ToList();
    }

    public static InsightDto ToInsightDto(this Insight insight)
    {
        ArgumentNullException.ThrowIfNull(insight);

        return new InsightDto
        {
            Code = insight.Code,
            Severity = insight.Severity.ToString(),
            Message = insight.Message,
            Context = insight.Context.ToInsightContextDto()
        };
    }

    public static InsightContextDto ToInsightContextDto(this InsightContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new InsightContextDto
        {
            ProgressDelta = context.ProgressDelta,
            ForecastRemainingDelta = context.ForecastRemainingDelta,
            PlanningQualityScore = context.PlanningQualityScore
        };
    }

    public static ProductDeliveryAnalyticsDto ToProductDeliveryAnalyticsDto(
        int productId,
        string productName,
        ProductAggregationResult product,
        SnapshotComparisonResult comparison,
        PlanningQualityResult planningQuality,
        InsightResult insights)
    {
        return new ProductDeliveryAnalyticsDto
        {
            ProductId = productId,
            ProductName = productName,
            Progress = product.ToProductProgressSummaryDto(),
            Comparison = comparison.ToSnapshotComparisonDto(),
            PlanningQuality = planningQuality.ToPlanningQualityDto(),
            Insights = insights.ToInsightDtos()
        };
    }
}
