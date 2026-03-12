using MudBlazor;
using PoTool.Client.Models;

namespace PoTool.Client.Components.Common;

public sealed record WorkspaceNavigationItemDefinition(
    string Label,
    string Route,
    string Icon,
    string AccentColor,
    IReadOnlyList<string> ActiveRoutePrefixes)
{
    public bool IsActive(string currentRelativePath) =>
        ActiveRoutePrefixes.Any(prefix => WorkspaceNavigationCatalog.PathMatchesPrefix(currentRelativePath, prefix));
}

public static class WorkspaceNavigationCatalog
{
    public static IReadOnlyList<WorkspaceNavigationItemDefinition> Items { get; } =
    [
        new(
            "Health",
            WorkspaceRoutes.HealthWorkspace,
            Icons.Material.Filled.HealthAndSafety,
            "var(--color-success)",
            [
                WorkspaceRoutes.HealthWorkspace,
                WorkspaceRoutes.ValidationTriage,
                WorkspaceRoutes.ValidationQueue,
                WorkspaceRoutes.ValidationFix,
                WorkspaceRoutes.BacklogOverview,
                WorkspaceRoutes.BugOverview,
                WorkspaceRoutes.BugDetail
            ]),
        new(
            "Delivery",
            WorkspaceRoutes.DeliveryWorkspace,
            Icons.Material.Filled.CheckCircle,
            "var(--color-info)",
            [
                WorkspaceRoutes.DeliveryWorkspace,
                WorkspaceRoutes.SprintDelivery,
                WorkspaceRoutes.SprintExecution,
                WorkspaceRoutes.SprintTrend,
                WorkspaceRoutes.SprintTrendActivity
            ]),
        new(
            "Trends",
            WorkspaceRoutes.TrendsWorkspace,
            Icons.Material.Filled.Timeline,
            "var(--color-trends)",
            [
                WorkspaceRoutes.TrendsWorkspace,
                WorkspaceRoutes.PrOverview,
                WorkspaceRoutes.PrDeliveryInsights,
                WorkspaceRoutes.PipelineInsights,
                WorkspaceRoutes.DependencyOverview,
                WorkspaceRoutes.PortfolioProgress
            ]),
        new(
            "Planning",
            WorkspaceRoutes.PlanningWorkspace,
            Icons.Material.Filled.CalendarMonth,
            "var(--color-planning)",
            [
                WorkspaceRoutes.PlanningWorkspace,
                WorkspaceRoutes.ProductRoadmaps,
                WorkspaceRoutes.PlanBoard
            ])
    ];

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var separatorIndex = path.IndexOfAny(['?', '#']);
        var trimmedPath = separatorIndex >= 0 ? path[..separatorIndex] : path;

        return trimmedPath.Trim().Trim('/').ToLowerInvariant();
    }

    public static bool PathMatchesPrefix(string currentRelativePath, string routePrefix)
    {
        var normalizedCurrentPath = NormalizePath(currentRelativePath);
        var normalizedPrefix = NormalizePath(routePrefix);

        if (string.IsNullOrEmpty(normalizedCurrentPath) || string.IsNullOrEmpty(normalizedPrefix))
        {
            return false;
        }

        return normalizedCurrentPath.Equals(normalizedPrefix, StringComparison.OrdinalIgnoreCase) ||
               normalizedCurrentPath.StartsWith($"{normalizedPrefix}/", StringComparison.OrdinalIgnoreCase);
    }
}
