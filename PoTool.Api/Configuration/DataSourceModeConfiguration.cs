namespace PoTool.Api.Configuration;

/// <summary>
/// Centralized configuration for DataSourceMode route intent rules.
/// Separates cache-only analytical/workspace reads from live-allowed onboarding,
/// configuration, discovery, and sync/write routes.
/// </summary>
public static class DataSourceModeConfiguration
{
    private const string WorkItemsRoutePrefix = "/api/workitems/";
    private static readonly string[] MiddlewareManagedRoutePrefixes =
    [
        "/api",
        "/health",
        "/hubs"
    ];
    public const string RouteIntentContextItemKey = "DataSourceMode.RouteIntent";
    public const string ResolvedModeContextItemKey = "DataSourceMode.ResolvedMode";

    public enum RouteIntent
    {
        Unknown = 0,
        LiveAllowed = 1,
        CacheOnlyAnalyticalRead = 2,
        BlockedAmbiguous = 3
    }

    /// <summary>
    /// Routes that are explicitly allowed to use Live mode.
    /// These are onboarding, configuration, discovery, sync, and administrative endpoints.
    /// Explicit live routes are matched before cache-only prefixes so discovery endpoints under
    /// broader controller prefixes (for example /api/workitems/area-paths/from-tfs) stay live.
    /// </summary>
    public static readonly HashSet<string> LiveModeAllowedRoutePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Settings and configuration
        "/api/settings",
        "/api/tfsconfig",
        "/api/bugtriage",
        "/api/healthcalculation",
        "/api/roadmapsnapshots",
        "/api/sprints",
        "/api/startup",
        "/api/triagetags",
        
        // Profile and team management
        "/api/profiles",
        "/api/teams",
        "/api/products",
        "/api/projects",
        "/api/portfolio/snapshots",
        "/api/repositories",

        // Data source mode management
        "/api/datasource",
        
        // Cache sync control (writes to cache from TFS)
        "/api/cachesync",

        // SignalR hub endpoints
        "/hubs",
        
        // Health and diagnostics
        "/health"
    };

    /// <summary>
    /// Exact routes that are explicitly allowed to use Live mode.
    /// Work item routes are listed here when they are discovery, validation, static configuration,
    /// or write/sync endpoints that must not be forced through cache-only guardrails.
    /// </summary>
    public static readonly HashSet<string> LiveModeAllowedExactRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Pipeline configuration/discovery
        "/api/pipelines/definitions",

        // TFS discovery and validation (Settings/Product setup use cases)
        "/api/workitems/area-paths/from-tfs",
        "/api/workitems/goals/from-tfs",
        "/api/workitems/validate",

        // Work item sync/write routes
        "/api/workitems/by-root-ids/refresh-from-tfs",
        "/api/workitems/fix-validation-violations",
        "/api/workitems/bulk-assign-effort",

        // Work item static configuration/supporting routes that should not require cache
        "/api/workitems/bug-severity-options",

        // TFS verification endpoints
        "/api/tfsvalidate",
        "/api/tfsverify",
        "/api/tfs/verify",
        "/api/tfs/validate"
    };

    /// <summary>
    /// Routes that represent analytical/workspace reads and therefore require cached data.
    /// These routes must never silently fall back to Live mode.
    /// </summary>
    public static readonly HashSet<string> CacheModeRequiredRoutePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/portfolio",
        "/api/workitems",
        "/api/pullrequests",
        "/api/pipelines",
        "/api/releaseplanning",
        "/api/filtering",
        "/api/metrics",
        "/api/buildquality"
    };

    /// <summary>
    /// Resolves the route intent for the current request path.
    /// </summary>
    public static RouteIntent GetRouteIntent(string? path)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null)
        {
            return RouteIntent.Unknown;
        }

        if (IsBlockedAmbiguousRoute(normalizedPath))
        {
            return RouteIntent.BlockedAmbiguous;
        }

        if (IsCacheOnlyProjectPlanningSummaryRoute(normalizedPath))
        {
            return RouteIntent.CacheOnlyAnalyticalRead;
        }

        if (LiveModeAllowedExactRoutes.Contains(normalizedPath) ||
            LiveModeAllowedRoutePrefixes.Any(route =>
                HasPathPrefix(normalizedPath, route)))
        {
            return RouteIntent.LiveAllowed;
        }

        if (IsLiveAllowedWorkItemDetailRoute(normalizedPath))
        {
            return RouteIntent.LiveAllowed;
        }

        if (CacheModeRequiredRoutePrefixes.Any(route =>
                HasPathPrefix(normalizedPath, route)))
        {
            return RouteIntent.CacheOnlyAnalyticalRead;
        }

        return RouteIntent.Unknown;
    }

    /// <summary>
    /// Resolves the route intent and throws when the route is not explicitly classified.
    /// </summary>
    public static RouteIntent ResolveRouteIntentOrThrow(string? path)
    {
        var intent = GetRouteIntent(path);
        return intent == RouteIntent.Unknown
            ? throw new PoTool.Api.Exceptions.RouteNotClassifiedException(path)
            : intent;
    }

    /// <summary>
    /// Checks if a route is an analytical/workspace read that requires cached data.
    /// </summary>
    public static bool RequiresCache(string? path)
    {
        return GetRouteIntent(path) == RouteIntent.CacheOnlyAnalyticalRead;
    }

    /// <summary>
    /// Checks if a route is explicitly allowed to use Live mode.
    /// </summary>
    public static bool IsLiveModeAllowed(string? path)
    {
        return GetRouteIntent(path) == RouteIntent.LiveAllowed;
    }

    /// <summary>
    /// Backward-compatible alias for older workspace-route checks.
    /// </summary>
    public static bool IsWorkspaceRoute(string? path)
    {
        return RequiresCache(path);
    }

    /// <summary>
    /// Returns whether the request path should bypass DataSourceModeMiddleware classification.
    /// </summary>
    public static bool ShouldBypassMiddleware(string? path)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null)
        {
            return true;
        }

        return !MiddlewareManagedRoutePrefixes.Any(prefix => HasPathPrefix(normalizedPath, prefix));
    }

    /// <summary>
    /// Returns whether the route is intentionally blocked because it mixes cache-only and live behavior.
    /// </summary>
    public static bool IsBlockedAmbiguousRoute(string? path)
    {
        // TODO: Requires endpoint split; see "Deferred Work" in
        // docs/analysis/datasource-enforcement.md.
        // State timeline currently mixes cached work item reads with live revision retrieval and
        // is intentionally blocked at runtime.
        return !string.IsNullOrEmpty(path) &&
               IsWorkItemDetailRoute(path, "/state-timeline");
    }

    /// <summary>
    /// Gets the current block reason for an ambiguous route.
    /// </summary>
    public static string? GetBlockedRouteReason(string? path)
    {
        return IsBlockedAmbiguousRoute(path)
            ? "Requires endpoint split; see Deferred Work in docs/analysis/datasource-enforcement.md."
            : null;
    }

    private static bool IsLiveAllowedWorkItemDetailRoute(string path)
    {
        return IsWorkItemDetailRoute(path, "/refresh-from-tfs")
            || IsWorkItemDetailRoute(path, "/tags")
            || IsWorkItemDetailRoute(path, "/title-description")
            || IsWorkItemDetailRoute(path, "/backlog-priority")
            || IsWorkItemDetailRoute(path, "/iteration-path")
            || IsWorkItemDetailRoute(path, "/revisions");
    }

    private static bool IsCacheOnlyProjectPlanningSummaryRoute(string path)
    {
        const string prefix = "/api/projects/";
        const string suffix = "/planning-summary";

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
            path.Length <= prefix.Length + suffix.Length)
        {
            return false;
        }

        var aliasSegmentLength = path.Length - prefix.Length - suffix.Length;
        return aliasSegmentLength > 0;
    }

    private static bool IsWorkItemDetailRoute(string path, string suffix)
    {
        if (!path.StartsWith(WorkItemsRoutePrefix, StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
            path.Length <= WorkItemsRoutePrefix.Length + suffix.Length)
        {
            return false;
        }

        var idSegmentLength = path.Length - WorkItemsRoutePrefix.Length - suffix.Length;
        var idSegment = path.AsSpan(WorkItemsRoutePrefix.Length, idSegmentLength);
        return int.TryParse(idSegment, out _);
    }

    /// <summary>
    /// Matches only whole route segments so managed prefixes such as /api do not accidentally
    /// classify unrelated paths like /apiary.
    /// </summary>
    private static bool HasPathPrefix(string path, string prefix)
    {
        return path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes request paths before classification so trailing slashes and missing leading
    /// slashes do not create separate routing behaviors for the same endpoint.
    /// </summary>
    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim();
        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = $"/{normalizedPath}";
        }

        if (normalizedPath.Length > 1)
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        return normalizedPath.Length == 0 ? "/" : normalizedPath;
    }
}
