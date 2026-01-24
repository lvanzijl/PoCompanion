using PoTool.Api.Configuration;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Api.Middleware;

/// <summary>
/// Middleware that sets the DataSourceMode (Live or Cache) based on the current route and ProductOwner's cache state.
/// This is the core fix for ensuring workspace routes use cached data when available.
/// Uses DataSourceModeConfiguration for centralized route rules.
/// </summary>
public sealed class DataSourceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DataSourceModeMiddleware> _logger;

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
        var isWorkspaceRoute = DataSourceModeConfiguration.IsWorkspaceRoute(path);

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
}
