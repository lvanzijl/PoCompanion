using PoTool.Core.Domain.DeliveryTrends.Models;
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
            TotalEffort = featureProgress.TotalScopeStoryPoints,
            DoneEffort = featureProgress.DeliveredStoryPoints,
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
            featureProgress.TotalEffort,
            featureProgress.DoneEffort,
            featureProgress.DonePbiCount,
            featureProgress.IsDone,
            featureProgress.SprintCompletedEffort,
            new ProgressionDelta(featureProgress.SprintProgressionDelta),
            featureProgress.SprintEffortDelta,
            featureProgress.SprintCompletedPbiCount,
            featureProgress.SprintCompletedInSprint);
    }

    public static EpicProgressDto ToEpicProgressDto(this EpicProgress epicProgress)
    {
        return new EpicProgressDto
        {
            EpicId = epicProgress.EpicId,
            EpicTitle = epicProgress.EpicTitle,
            ProductId = epicProgress.ProductId,
            ProgressPercent = epicProgress.ProgressPercent,
            TotalEffort = epicProgress.TotalScopeStoryPoints,
            DoneEffort = epicProgress.DeliveredStoryPoints,
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
}
