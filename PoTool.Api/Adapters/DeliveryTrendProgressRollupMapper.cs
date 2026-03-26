using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Adapters;

internal static class DeliveryTrendProgressRollupMapper
{
    public static FeatureProgressDto ToFeatureProgressDto(
        this FeatureProgress featureProgress,
        IReadOnlyList<CompletedPbiDto> completedPbis)
    {
        return new FeatureProgressDto
        {
            FeatureId = featureProgress.FeatureId,
            FeatureTitle = featureProgress.FeatureTitle,
            EpicId = featureProgress.EpicId,
            EpicTitle = featureProgress.EpicTitle,
            ProductId = featureProgress.ProductId,
            ProgressPercent = featureProgress.ProgressPercent,
            CalculatedProgress = featureProgress.CalculatedProgress,
            Override = featureProgress.Override,
            EffectiveProgress = featureProgress.EffectiveProgress,
            ForecastConsumedEffort = featureProgress.ForecastConsumedEffort,
            ForecastRemainingEffort = featureProgress.ForecastRemainingEffort,
            ValidationSignals = featureProgress.ValidationSignals,
            TotalStoryPoints = featureProgress.TotalScopeStoryPoints,
            DoneStoryPoints = featureProgress.DeliveredStoryPoints,
            DonePbiCount = featureProgress.DonePbiCount,
            IsDone = featureProgress.IsDone,
            SprintCompletedEffort = featureProgress.SprintDeliveredStoryPoints,
            SprintProgressionDelta = featureProgress.SprintProgressionDelta.Percentage,
            SprintEffortDelta = featureProgress.SprintEffortDelta,
            SprintCompletedPbiCount = featureProgress.SprintCompletedPbiCount,
            SprintCompletedInSprint = featureProgress.SprintCompletedInSprint,
            CompletedPbis = completedPbis
        };
    }

    public static FeatureProgress ToFeatureProgress(
        this FeatureProgressDto featureProgress,
        IReadOnlyDictionary<int, DeliveryTrendWorkItem> workItemsById)
    {
        string? epicTitle = featureProgress.EpicTitle;
        if (featureProgress.EpicId.HasValue
            && string.IsNullOrWhiteSpace(epicTitle)
            && workItemsById.TryGetValue(featureProgress.EpicId.Value, out var epicWorkItem))
        {
            epicTitle = epicWorkItem.Title;
        }

        return new FeatureProgress(
            featureProgress.FeatureId,
            featureProgress.FeatureTitle,
            featureProgress.ProductId,
            featureProgress.EpicId,
            epicTitle,
            featureProgress.ProgressPercent,
            featureProgress.TotalStoryPoints,
            featureProgress.DoneStoryPoints,
            featureProgress.DonePbiCount,
            featureProgress.IsDone,
            featureProgress.SprintCompletedEffort,
            new ProgressionDelta(featureProgress.SprintProgressionDelta),
            featureProgress.SprintEffortDelta,
            featureProgress.SprintCompletedPbiCount,
            featureProgress.SprintCompletedInSprint,
            featureProgress.CalculatedProgress,
            featureProgress.Override,
            featureProgress.EffectiveProgress,
            featureProgress.ValidationSignals,
            featureProgress.ForecastConsumedEffort,
            featureProgress.ForecastRemainingEffort);
    }

    public static EpicProgressDto ToEpicProgressDto(this EpicProgress epicProgress)
    {
        return new EpicProgressDto
        {
            EpicId = epicProgress.EpicId,
            EpicTitle = epicProgress.EpicTitle,
            ProductId = epicProgress.ProductId,
            ProgressPercent = epicProgress.ProgressPercent,
            TotalStoryPoints = epicProgress.TotalScopeStoryPoints,
            DoneStoryPoints = epicProgress.DeliveredStoryPoints,
            FeatureCount = epicProgress.FeatureCount,
            DoneFeatureCount = epicProgress.DoneFeatureCount,
            DonePbiCount = epicProgress.DonePbiCount,
            IsDone = epicProgress.IsDone,
            SprintCompletedEffort = epicProgress.SprintDeliveredStoryPoints,
            SprintProgressionDelta = epicProgress.SprintProgressionDelta.Percentage,
            SprintEffortDelta = epicProgress.SprintEffortDelta,
            SprintCompletedPbiCount = epicProgress.SprintCompletedPbiCount,
            SprintCompletedFeatureCount = epicProgress.SprintCompletedFeatureCount
        };
    }

    public static EpicProgress ToEpicProgress(this EpicProgressDto epicProgress)
    {
        return new EpicProgress(
            epicProgress.EpicId,
            epicProgress.EpicTitle,
            epicProgress.ProductId,
            epicProgress.ProgressPercent,
            epicProgress.TotalStoryPoints,
            epicProgress.DoneStoryPoints,
            epicProgress.FeatureCount,
            epicProgress.DoneFeatureCount,
            epicProgress.DonePbiCount,
            epicProgress.IsDone,
            epicProgress.SprintCompletedEffort,
            new ProgressionDelta(epicProgress.SprintProgressionDelta),
            epicProgress.SprintEffortDelta,
            epicProgress.SprintCompletedPbiCount,
            epicProgress.SprintCompletedFeatureCount);
    }

    public static IReadOnlyDictionary<int, ProductDeliveryProgressSummary> ToProductDeliveryProgressSummaries(
        this IReadOnlyList<EpicProgressDto> epicProgress)
    {
        ArgumentNullException.ThrowIfNull(epicProgress);

        return DeliveryProgressSummaryCalculator.ComputeProductSummaries(
            epicProgress.Select(progress => progress.ToEpicProgress()).ToList());
    }
}
