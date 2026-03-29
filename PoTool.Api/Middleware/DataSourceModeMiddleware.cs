using PoTool.Api.Configuration;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace PoTool.Api.Middleware;

/// <summary>
/// Middleware that sets the DataSourceMode (Live or Cache) based on explicit route intent
/// and ProductOwner cache state.
/// Cache-only analytical routes are blocked until a successful sync exists.
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
        var routeIntent = DataSourceModeConfiguration.GetRouteIntent(path);

        if (routeIntent == DataSourceModeConfiguration.RouteIntent.CacheOnlyAnalyticalRead)
        {
            _logger.LogDebug("Cache-only analytical route detected: {Path}", path);

            var productOwnerId = await profileProvider.GetCurrentProductOwnerIdAsync(context.RequestAborted);
            if (productOwnerId.HasValue)
            {
                var mode = await modeProvider.GetModeAsync(productOwnerId.Value, context.RequestAborted);

                if (mode == DataSourceMode.Cache)
                {
                    modeProvider.SetCurrentMode(DataSourceMode.Cache);

                    _logger.LogInformation(
                        "DataSourceMode set to Cache for analytical route {Path} (ProductOwner: {ProductOwnerId})",
                        path,
                        productOwnerId.Value);

                    await _next(context);
                    return;
                }

                _logger.LogWarning(
                    "Blocking analytical route {Path} for ProductOwner {ProductOwnerId} because no successful cache sync is available",
                    path,
                    productOwnerId.Value);

                await WriteCacheNotReadyResponseAsync(
                    context,
                    $"Analytical endpoint {path} requires cached data. Run a successful sync for the active profile before requesting this resource.");
                return;
            }

            _logger.LogWarning(
                "Blocking analytical route {Path} because no active profile is selected",
                path);

            await WriteCacheNotReadyResponseAsync(
                context,
                $"Analytical endpoint {path} requires an active profile with a successful cache sync.");
            return;
        }

        if (routeIntent == DataSourceModeConfiguration.RouteIntent.LiveAllowed)
        {
            _logger.LogDebug("Live-allowed route detected: {Path}, using Live mode", path);
        }
        else
        {
            _logger.LogDebug("Unclassified route detected: {Path}, defaulting to Live mode", path);
        }

        modeProvider.SetCurrentMode(DataSourceMode.Live);
        await _next(context);
    }

    private static async Task WriteCacheNotReadyResponseAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Cache not ready",
            Detail = detail
        };

        await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
    }
}
