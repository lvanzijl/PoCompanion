namespace PoTool.Api.Configuration;

/// <summary>
/// Centralized configuration for DataSourceMode route rules.
/// Defines which routes are allowed to use Live mode and which must use Cache mode.
/// </summary>
public static class DataSourceModeConfiguration
{
    /// <summary>
    /// Routes that are explicitly allowed to use Live mode.
    /// These are typically Settings, configuration, and administrative endpoints.
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
    /// Routes that MUST use Cache mode when cache is available.
    /// These are workspace-facing endpoints that should never hit TFS live during normal operation.
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
    /// Checks if a route is a workspace route that must use Cache mode.
    /// </summary>
    /// <param name="path">The request path to check</param>
    /// <returns>True if the route is a workspace route, false otherwise</returns>
    public static bool IsWorkspaceRoute(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return CacheModeRequiredRoutes.Any(route => 
            path.StartsWith(route, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a route is explicitly allowed to use Live mode.
    /// </summary>
    /// <param name="path">The request path to check</param>
    /// <returns>True if the route is allowed to use Live mode, false otherwise</returns>
    public static bool IsLiveModeAllowed(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return LiveModeAllowedRoutes.Any(route => 
            path.StartsWith(route, StringComparison.OrdinalIgnoreCase));
    }
}
