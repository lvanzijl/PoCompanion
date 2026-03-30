using PoTool.Api.Configuration;
using PoTool.Api.Exceptions;
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
        var path = context.Request.Path.Value ?? string.Empty;
        DataSourceModeConfiguration.RouteIntent routeIntent;

        try
        {
            routeIntent = DataSourceModeConfiguration.ResolveRouteIntentOrThrow(path);
        }
        catch (RouteNotClassifiedException)
        {
            _logger.LogError(
                "[Violation] Route={Route} Mode=Unknown AttemptedProvider=None Action=Blocked",
                path);
            throw;
        }

        context.Items[DataSourceModeConfiguration.RouteIntentContextItemKey] = routeIntent;

        if (routeIntent == DataSourceModeConfiguration.RouteIntent.BlockedAmbiguous)
        {
            var reason = DataSourceModeConfiguration.GetBlockedRouteReason(path) ?? "Requires endpoint split; see docs/analysis/datasource-enforcement.md#deferred-work.";
            _logger.LogError(
                "[Violation] Route={Route} Mode=BlockedAmbiguous AttemptedProvider=Live Action=Blocked Reason={Reason}",
                path,
                reason);
            throw new NotSupportedException(
                $"Route {path} is blocked because it mixes cached and live behavior. {reason}");
        }

        if (routeIntent == DataSourceModeConfiguration.RouteIntent.CacheOnlyAnalyticalRead)
        {
            context.Items[DataSourceModeConfiguration.ResolvedModeContextItemKey] = DataSourceMode.Cache;
            _logger.LogDebug("Cache-only analytical route detected: {Path}", path);

            var productOwnerId = await profileProvider.GetCurrentProductOwnerIdAsync(context.RequestAborted);
            if (productOwnerId.HasValue)
            {
                var mode = await modeProvider.GetModeAsync(productOwnerId.Value, context.RequestAborted);

                if (mode == DataSourceMode.Cache)
                {
                    modeProvider.SetCurrentMode(DataSourceMode.Cache);

                    _logger.LogInformation(
                        "[DataSourceMode] Route={Route} Mode=CacheOnly Provider=Cache ProductOwnerId={ProductOwnerId}",
                        path,
                        productOwnerId.Value);

                    await _next(context);
                    return;
                }

                _logger.LogWarning(
                    "[Violation] Route={Route} Mode=CacheOnly AttemptedProvider=Live Action=Blocked ProductOwnerId={ProductOwnerId}",
                    path,
                    productOwnerId.Value);

                await WriteCacheNotReadyResponseAsync(
                    context,
                    $"Analytical endpoint {path} requires cached data. Run a successful sync for the active profile before requesting this resource.");
                return;
            }

            _logger.LogWarning(
                "[Violation] Route={Route} Mode=CacheOnly AttemptedProvider=Live Action=Blocked",
                path);

            await WriteCacheNotReadyResponseAsync(
                context,
                $"Analytical endpoint {path} requires an active profile with a successful cache sync.");
            return;
        }

        context.Items[DataSourceModeConfiguration.ResolvedModeContextItemKey] = DataSourceMode.Live;
        modeProvider.SetCurrentMode(DataSourceMode.Live);
        _logger.LogInformation(
            "[DataSourceMode] Route={Route} Mode=LiveAllowed Provider=Live",
            path);
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
