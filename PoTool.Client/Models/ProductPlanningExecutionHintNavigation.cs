using PoTool.Shared.Planning;

namespace PoTool.Client.Models;

public static class ProductPlanningExecutionHintNavigation
{
    public static string ResolveRoute(ProductPlanningExecutionHintDto hint)
    {
        ArgumentNullException.ThrowIfNull(hint);

        return hint.AnomalyKey switch
        {
            "completion-below-typical" => WorkspaceRoutes.SprintExecution,
            "completion-variability" => WorkspaceRoutes.DeliveryTrends,
            "spillover-increase" => WorkspaceRoutes.SprintExecution,
            _ => throw new ArgumentOutOfRangeException(nameof(hint), hint.AnomalyKey, "Unknown execution hint anomaly key.")
        };
    }

    public static FilterState BuildTargetState(
        ProductPlanningExecutionHintDto hint,
        FilterState currentState,
        int? productId)
    {
        ArgumentNullException.ThrowIfNull(hint);
        ArgumentNullException.ThrowIfNull(currentState);

        var route = ResolveRoute(hint);
        var productIds = productId.HasValue
            ? new[] { productId.Value }
            : currentState.ProductIds.ToArray();
        var time = route == WorkspaceRoutes.DeliveryTrends
            ? BuildDeliveryTrendsTimeSelection(currentState.Time, hint.SprintId)
            : new FilterTimeSelection(FilterTimeMode.Sprint, SprintId: hint.SprintId);

        return new FilterState(
            productIds,
            currentState.ProjectIds,
            hint.TeamId,
            time);
    }

    private static FilterTimeSelection BuildDeliveryTrendsTimeSelection(FilterTimeSelection currentTime, int sprintId)
    {
        if (currentTime.Mode == FilterTimeMode.Range && currentTime.IsResolved)
        {
            return new FilterTimeSelection(
                FilterTimeMode.Range,
                StartSprintId: currentTime.StartSprintId,
                EndSprintId: currentTime.EndSprintId);
        }

        return new FilterTimeSelection(
            FilterTimeMode.Range,
            StartSprintId: sprintId,
            EndSprintId: sprintId);
    }
}
