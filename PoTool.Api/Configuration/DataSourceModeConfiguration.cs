namespace PoTool.Api.Configuration;

/// <summary>
/// Centralized configuration for DataSourceMode route intent rules.
/// Separates cache-only analytical/workspace reads from live-allowed onboarding,
/// configuration, discovery, and sync/write routes.
/// </summary>
public static class DataSourceModeConfiguration
{
    public enum RouteIntent
    {
        Unknown = 0,
        LiveAllowed = 1,
        CacheOnlyAnalyticalRead = 2
    }

    /// <summary>
    /// Routes that are explicitly allowed to use Live mode.
    /// These are onboarding, configuration, discovery, sync, and administrative endpoints.
    /// Explicit live routes are matched before cache-only prefixes so discovery endpoints under
    /// broader controller prefixes (for example /api/workitems/area-paths-from-tfs) stay live.
    /// </summary>
    public static readonly HashSet<string> LiveModeAllowedRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Settings and configuration
        "/api/settings",
        "/api/tfsconfig",
        "/api/startup",
        
        // Profile and team management
        "/api/profiles",
        "/api/teams",
        "/api/products",
        "/api/repositories",

        // Pipeline configuration/discovery
        "/api/pipelines/definitions",
        
        // TFS discovery and validation (Settings use cases)
        "/api/workitems/area-paths-from-tfs",
        "/api/workitems/goals-from-tfs",
        "/api/workitems/validate",
        "/api/workitems/revisions",
        
        // TFS verification endpoints
        "/api/tfs/verify",
        "/api/tfs/validate",
        
        // Data source mode management
        "/api/datasource",
        
        // Cache sync control (writes to cache from TFS)
        "/api/cachesync",
        
        // Health and diagnostics
        "/health"
    };

    /// <summary>
    /// Routes that represent analytical/workspace reads and therefore require cached data.
    /// These routes must never silently fall back to Live mode.
    /// </summary>
    public static readonly HashSet<string> CacheModeRequiredRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/workitems",
        "/api/pullrequests",
        "/api/pipelines",
        "/api/releaseplanning",
        "/api/filtering",
        "/api/metrics"
    };

    /// <summary>
    /// Resolves the route intent for the current request path.
    /// </summary>
    public static RouteIntent GetRouteIntent(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return RouteIntent.Unknown;
        }

        if (LiveModeAllowedRoutes.Any(route =>
                path.StartsWith(route, StringComparison.OrdinalIgnoreCase)))
        {
            return RouteIntent.LiveAllowed;
        }

        if (CacheModeRequiredRoutes.Any(route =>
                path.StartsWith(route, StringComparison.OrdinalIgnoreCase)))
        {
            return RouteIntent.CacheOnlyAnalyticalRead;
        }

        return RouteIntent.Unknown;
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
}
