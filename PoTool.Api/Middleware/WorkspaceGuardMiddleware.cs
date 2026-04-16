using PoTool.Api.Configuration;
using PoTool.Core.Configuration;

namespace PoTool.Api.Middleware;

/// <summary>
/// Middleware that throws an exception if a cache-only analytical route attempts to use Live mode.
/// This is a defensive architecture guard on top of request-time mode selection.
/// Uses DataSourceModeConfiguration for centralized route rules.
/// </summary>
public sealed class WorkspaceGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WorkspaceGuardMiddleware> _logger;

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
        await _next(context);

        var path = context.Request.Path.Value;
        var requiresCache = DataSourceModeEndpointMetadataResolver.RequiresCache(context.GetEndpoint(), path);

        if (requiresCache && modeProvider.Mode == DataSourceMode.Live)
        {
            // This is a runtime architecture violation - analytical routes must remain cache-backed
            _logger.LogError(
                "WORKSPACE GUARD VIOLATION: Cache-only analytical route {Path} used Live mode. " +
                "This indicates cache is not available or mode selection failed. " +
                "Analytical routes MUST use Cache mode.",
                path);

            throw new InvalidOperationException(
                $"Cache-only analytical route {path} must use Cache mode. " +
                $"Current mode: {modeProvider.Mode}. " +
                $"This indicates a cache-boundary enforcement bug.");
        }
    }
}
