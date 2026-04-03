using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public static class GlobalFilterPageCatalog
{
    public static bool TryCreateUsageReport(string? uri, int? activeProfileId, out GlobalFilterUsageReport? report)
    {
        report = null;

        var route = NormalizeRoute(uri);
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        if (!TryGetPageDefinition(route, out var definition))
        {
            return false;
        }

        var context = WorkspaceQueryContextHelper.Parse(uri);
        var productIds = ResolveProductIds(route, context.ProductId);
        var projectAliases = ResolveProjectAliases(route, context.ProjectAlias);
        var teamId = context.TeamId;
        var hasSprintSelection = context.SprintId.HasValue || context.FromSprintId.HasValue || context.ToSprintId.HasValue;
        var missingTeam = definition.RequiresTeam && !teamId.HasValue;
        var missingSprint = definition.RequiresSprint && !hasSprintSelection;

        report = new GlobalFilterUsageReport(
            definition.PageName,
            route,
            definition.UsesProduct,
            definition.UsesProject,
            definition.UsesTeam,
            definition.UsesTime,
            productIds,
            projectAliases,
            teamId,
            definition.TimeMode,
            ResolveTimeValue(definition.TimeMode, context),
            missingTeam,
            missingSprint,
            activeProfileId,
            DateTimeOffset.UtcNow);

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
            "home" => new("HomePage", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            "home/health" => new("HealthWorkspace", false, false, false, false, GlobalFilterTimeMode.Snapshot),
            "home/health/overview" => new("HealthOverviewPage", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            "home/health/backlog-health" or "home/backlog-overview" => new("BacklogOverviewPage", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            "home/changes" => new("HomeChanges", false, false, false, false, GlobalFilterTimeMode.Snapshot),
            "home/delivery" => new("DeliveryWorkspace", false, false, false, false, GlobalFilterTimeMode.Snapshot),
            "home/delivery/portfolio" => new("PortfolioDelivery", true, false, true, true, GlobalFilterTimeMode.Trend, RequiresTeam: true, RequiresSprint: true),
            "home/delivery/execution" => new("SprintExecution", true, false, true, true, GlobalFilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/delivery/sprint" or "home/sprint-trend" => new("SprintTrend", true, false, true, true, GlobalFilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/trends" => new("TrendsWorkspace", true, false, true, true, GlobalFilterTimeMode.Trend, RequiresTeam: true, RequiresSprint: true),
            "home/trends/delivery" => new("DeliveryTrends", true, false, true, true, GlobalFilterTimeMode.Trend, RequiresTeam: true, RequiresSprint: true),
            "home/portfolio-progress" => new("PortfolioProgressPage", true, false, true, true, GlobalFilterTimeMode.Trend, RequiresTeam: true, RequiresSprint: true),
            "home/pipeline-insights" => new("PipelineInsights", true, false, true, true, GlobalFilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/pull-requests" => new("PrOverview", false, false, true, true, GlobalFilterTimeMode.Rolling, RequiresTeam: true),
            "home/pr-delivery-insights" => new("PrDeliveryInsights", false, false, true, true, GlobalFilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            "home/bugs" => new("BugOverview", true, false, true, false, GlobalFilterTimeMode.Snapshot, RequiresTeam: false),
            "home/bugs/detail" => new("BugDetail", false, false, false, false, GlobalFilterTimeMode.Snapshot),
            "home/validation-triage" => new("ValidationTriagePage", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            "home/validation-queue" => new("ValidationQueuePage", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            "home/validation-fix" => new("ValidationFixPage", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            "home/planning" => new("PlanningWorkspace", false, true, false, false, GlobalFilterTimeMode.Snapshot),
            "planning/multi-product" => new("MultiProductPlanning", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            "planning/product-roadmaps" => new("ProductRoadmaps", false, true, false, false, GlobalFilterTimeMode.Snapshot),
            "planning/plan-board" => new("PlanBoard", true, true, false, true, GlobalFilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/product-roadmaps/", StringComparison.Ordinal) => new("ProductRoadmapEditor", true, true, false, false, GlobalFilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/", StringComparison.Ordinal) && route.EndsWith("/product-roadmaps", StringComparison.Ordinal) => new("ProductRoadmaps", false, true, false, false, GlobalFilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/", StringComparison.Ordinal) && route.EndsWith("/plan-board", StringComparison.Ordinal) => new("PlanBoard", true, true, false, true, GlobalFilterTimeMode.Snapshot),
            _ when route.StartsWith("planning/", StringComparison.Ordinal) && route.EndsWith("/overview", StringComparison.Ordinal) => new("ProjectPlanningOverview", false, true, false, false, GlobalFilterTimeMode.Snapshot),
            _ when route.StartsWith("home/delivery/sprint/activity/", StringComparison.Ordinal) || route.StartsWith("home/sprint-trend/activity/", StringComparison.Ordinal)
                => new("SprintTrendActivity", true, false, true, true, GlobalFilterTimeMode.Sprint, RequiresTeam: true, RequiresSprint: true),
            _ => null!
        };

        return definition is not null;
    }

    private static IReadOnlyList<string> ResolveProjectAliases(string route, string? queryProjectAlias)
    {
        if (!string.IsNullOrWhiteSpace(queryProjectAlias))
        {
            return new[] { queryProjectAlias };
        }

        if (!route.StartsWith("planning/", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3
            && !string.Equals(segments[1], "plan-board", StringComparison.Ordinal)
            && !string.Equals(segments[1], "product-roadmaps", StringComparison.Ordinal)
            && !string.Equals(segments[1], "multi-product", StringComparison.Ordinal))
        {
            return new[] { segments[1] };
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<int> ResolveProductIds(string route, int? queryProductId)
    {
        if (queryProductId.HasValue)
        {
            return new[] { queryProductId.Value };
        }

        if (route.StartsWith("planning/product-roadmaps/", StringComparison.Ordinal))
        {
            var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && int.TryParse(segments[2], out var productId))
            {
                return new[] { productId };
            }
        }

        return Array.Empty<int>();
    }

    private static string? ResolveTimeValue(GlobalFilterTimeMode timeMode, WorkspaceQueryContext context)
    {
        return timeMode switch
        {
            GlobalFilterTimeMode.Sprint when context.SprintId.HasValue => $"Sprint {context.SprintId.Value}",
            GlobalFilterTimeMode.Trend when context.FromSprintId.HasValue || context.ToSprintId.HasValue
                => $"{FormatSprintBoundary("from", context.FromSprintId)} → {FormatSprintBoundary("to", context.ToSprintId)}",
            GlobalFilterTimeMode.Rolling when context.SprintId.HasValue => $"Sprint {context.SprintId.Value}",
            GlobalFilterTimeMode.Snapshot => "Current page state",
            _ => null
        };
    }

    private static string FormatSprintBoundary(string label, int? sprintId)
        => sprintId.HasValue ? $"{label} {sprintId.Value}" : $"{label} ?";
}
