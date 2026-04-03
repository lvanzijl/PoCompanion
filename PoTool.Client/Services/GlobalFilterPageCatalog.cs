using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public static class GlobalFilterPageCatalog
{
    public static bool TryResolvePage(string? uri, out string route, out GlobalFilterPageDefinition? definition, out WorkspaceQueryContext context)
    {
        route = NormalizeRoute(uri);
        context = WorkspaceQueryContextHelper.Parse(uri);
        definition = null;
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        if (!TryGetPageDefinition(route, out definition))
        {
            return false;
        }

        return true;
    }

    private static string NormalizeRoute(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return string.Empty;
        }

        var relative = Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.PathAndQuery
            : uri;

        relative = relative.Split('#')[0];
        relative = relative.Split('?')[0];
        return relative.Trim('/').ToLowerInvariant();
    }

    private static bool TryGetPageDefinition(string route, out GlobalFilterPageDefinition definition)
    {
        definition = route switch
        {
            "home" => new("HomePage", true, true, false, false, FilterTimeMode.Snapshot),
            "home/health" => new("HealthWorkspace", false, false, false, false, FilterTimeMode.Snapshot),
            "home/health/overview" => new("HealthOverviewPage", true, true, false, false, FilterTimeMode.Snapshot),
            "home/health/backlog-health" or "home/backlog-overview" => new("BacklogOverviewPage", true, true, false, false, FilterTimeMode.Snapshot),
            "home/changes" => new("HomeChanges", false, false, false, false, FilterTimeMode.Snapshot),
            "home/delivery" => new("DeliveryWorkspace", false, false, false, false, FilterTimeMode.Snapshot),
            "home/delivery/portfolio" => new("PortfolioDelivery", true, false, true, true, FilterTimeMode.Range, RequiresTeam: true, RequiresSprint: true),
            "home/delivery/execution" => new("SprintExecution", true, false, true, true, FilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/delivery/sprint" or "home/sprint-trend" => new("SprintTrend", true, false, true, true, FilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/trends" => new("TrendsWorkspace", true, false, true, true, FilterTimeMode.Range, RequiresTeam: true, RequiresSprint: true),
            "home/trends/delivery" => new("DeliveryTrends", true, false, true, true, FilterTimeMode.Range, RequiresTeam: true, RequiresSprint: true),
            "home/portfolio-progress" => new("PortfolioProgressPage", true, false, true, true, FilterTimeMode.Range, RequiresTeam: true, RequiresSprint: true),
            "home/pipeline-insights" => new("PipelineInsights", true, false, true, true, FilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/pull-requests" => new("PrOverview", false, false, true, true, FilterTimeMode.Rolling, RequiresTeam: true),
            "home/pr-delivery-insights" => new("PrDeliveryInsights", false, false, true, true, FilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/bugs" => new("BugOverview", true, false, true, false, FilterTimeMode.Snapshot, RequiresTeam: false),
            "home/bugs/detail" => new("BugDetail", false, false, false, false, FilterTimeMode.Snapshot),
            "home/validation-triage" => new("ValidationTriagePage", true, true, false, false, FilterTimeMode.Snapshot),
            "home/validation-queue" => new("ValidationQueuePage", true, true, false, false, FilterTimeMode.Snapshot),
            "home/validation-fix" => new("ValidationFixPage", true, true, false, false, FilterTimeMode.Snapshot),
            "home/planning" => new("PlanningWorkspace", false, true, false, false, FilterTimeMode.Snapshot),
            "planning/multi-product" => new("MultiProductPlanning", true, true, false, false, FilterTimeMode.Snapshot),
            "planning/product-roadmaps" => new("ProductRoadmaps", false, true, false, false, FilterTimeMode.Snapshot),
            "planning/plan-board" => new("PlanBoard", true, true, false, true, FilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/product-roadmaps/", StringComparison.Ordinal) => new("ProductRoadmapEditor", true, true, false, false, FilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/", StringComparison.Ordinal) && route.EndsWith("/product-roadmaps", StringComparison.Ordinal) => new("ProductRoadmaps", false, true, false, false, FilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/", StringComparison.Ordinal) && route.EndsWith("/plan-board", StringComparison.Ordinal) => new("PlanBoard", true, true, false, true, FilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/", StringComparison.Ordinal) && route.EndsWith("/overview", StringComparison.Ordinal) => new("ProjectPlanningOverview", false, true, false, false, FilterTimeMode.Snapshot),
            _ when route.StartsWith("home/delivery/sprint/activity/", StringComparison.Ordinal) || route.StartsWith("home/sprint-trend/activity/", StringComparison.Ordinal)
                => new("SprintTrendActivity", true, false, true, true, FilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            _ => null!
        };

        return definition is not null;
    }

    public static string? ResolveRouteProjectAlias(string route)
    {
        if (!route.StartsWith("planning/", StringComparison.Ordinal))
        {
            return null;
        }

        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3
            && !string.Equals(segments[1], "plan-board", StringComparison.Ordinal)
            && !string.Equals(segments[1], "product-roadmaps", StringComparison.Ordinal)
            && !string.Equals(segments[1], "multi-product", StringComparison.Ordinal))
        {
            return segments[1];
        }

        return null;
    }

    public static int? ResolveRouteProductId(string route)
    {
        if (route.StartsWith("planning/product-roadmaps/", StringComparison.Ordinal))
        {
            var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && int.TryParse(segments[2], out var productId))
            {
                return productId;
            }
        }

        return null;
    }

    public static bool HasSprintSelection(WorkspaceQueryContext context, FilterLocalBridgeState? localState = null)
        => context.SprintId.HasValue
           || context.FromSprintId.HasValue
           || context.ToSprintId.HasValue
           || localState?.SprintId.HasValue == true
           || localState?.FromSprintId.HasValue == true
           || localState?.ToSprintId.HasValue == true;
}
