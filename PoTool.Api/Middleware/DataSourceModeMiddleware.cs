using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Api.Middleware;

/// <summary>
/// Middleware that sets the DataSourceMode (Live or Cache) based on the current route and ProductOwner's cache state.
/// This is the core fix for ensuring workspace routes use cached data when available.
/// </summary>
public sealed class DataSourceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DataSourceModeMiddleware> _logger;

    // Routes that should use cached data when cache is available
    private static readonly HashSet<string> WorkspaceRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/workitems",
        "/api/pullrequests",
        "/api/pipelines",
        "/api/releaseplanning",
        "/api/filtering"
    };

    public DataSourceModeMiddleware(
        RequestDelegate next,
        ILogger<DataSourceModeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IDataSourceModeProvider modeProvider,
        ICurrentProfileProvider profileProvider)
    {
        var path = context.Request.Path.Value;
        var isWorkspaceRoute = IsWorkspaceRoute(path);

        if (isWorkspaceRoute)
        {
            _logger.LogDebug("Workspace route detected: {Path}", path);

            // Get current ProductOwner ID
            var productOwnerId = await profileProvider.GetCurrentProductOwnerIdAsync(context.RequestAborted);

            if (productOwnerId.HasValue)
            {
                // Get the appropriate mode based on cache state
                var mode = await modeProvider.GetModeAsync(productOwnerId.Value, context.RequestAborted);
                
                // Set the mode for this request
                modeProvider.SetCurrentMode(mode);

                _logger.LogInformation(
                    "DataSourceMode set to {Mode} for workspace route {Path} (ProductOwner: {ProductOwnerId})",
                    mode, path, productOwnerId.Value);
            }
            else
            {
                _logger.LogWarning(
                    "No active ProductOwner found for workspace route {Path}, defaulting to Live mode",
                    path);
                
                // No active profile, use Live mode
                modeProvider.SetCurrentMode(DataSourceMode.Live);
            }
        }
        else
        {
            // Non-workspace routes (Settings, etc.) use Live mode
            _logger.LogDebug("Non-workspace route: {Path}, using Live mode", path);
            modeProvider.SetCurrentMode(DataSourceMode.Live);
        }

        await _next(context);
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
