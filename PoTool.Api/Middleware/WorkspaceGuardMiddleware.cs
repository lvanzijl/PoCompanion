using PoTool.Core.Configuration;

namespace PoTool.Api.Middleware;

/// <summary>
/// Development-only middleware that throws an exception if a workspace route attempts to use Live mode.
/// This provides fast feedback during development if mode selection fails.
/// </summary>
public sealed class WorkspaceGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WorkspaceGuardMiddleware> _logger;

    // Routes that MUST use Cache mode (same as DataSourceModeMiddleware)
    private static readonly HashSet<string> WorkspaceRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/workitems",
        "/api/pullrequests",
        "/api/pipelines",
        "/api/releaseplanning",
        "/api/filtering"
    };

    public WorkspaceGuardMiddleware(
        RequestDelegate next,
        ILogger<WorkspaceGuardMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IDataSourceModeProvider modeProvider)
    {
        // First, let the request be processed (mode will be set by DataSourceModeMiddleware)
        await _next(context);

        // After request processing, check if a workspace route used Live mode
        var path = context.Request.Path.Value;
        var isWorkspaceRoute = IsWorkspaceRoute(path);

        if (isWorkspaceRoute && modeProvider.Mode == DataSourceMode.Live)
        {
            // This is a development-time error - workspace routes should use Cache mode when available
            _logger.LogError(
                "WORKSPACE GUARD VIOLATION: Workspace route {Path} used Live mode. " +
                "This indicates cache is not available or mode selection failed. " +
                "Workspace routes MUST use Cache mode when cache is available.",
                path);

            // In development, throw exception to fail fast
            throw new InvalidOperationException(
                $"Workspace route {path} must use Cache mode. " +
                $"Current mode: {modeProvider.Mode}. " +
                $"This is a development-time check to ensure workspace cache boundary is enforced.");
        }
    }

    private static bool IsWorkspaceRoute(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return WorkspaceRoutes.Any(route => path.StartsWith(route, StringComparison.OrdinalIgnoreCase));
    }
}
