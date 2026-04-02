using PoTool.Api.Configuration;
using PoTool.Api.Exceptions;
using PoTool.Core.Configuration;

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
        IDataSourceModeProvider modeProvider)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (DataSourceModeConfiguration.ShouldBypassMiddleware(path))
        {
            _logger.LogDebug("Bypassing data source classification for non-managed route {Path}", path);
            await _next(context);
            return;
        }

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
            modeProvider.SetCurrentMode(DataSourceMode.Cache);
            _logger.LogDebug("Cache-only analytical route detected: {Path}", path);
            _logger.LogInformation(
                "[DataSourceMode] Route={Route} Mode=CacheOnly Provider=Cache",
                path);
            await _next(context);
            return;
        }

        context.Items[DataSourceModeConfiguration.ResolvedModeContextItemKey] = DataSourceMode.Live;
        modeProvider.SetCurrentMode(DataSourceMode.Live);
        _logger.LogInformation(
            "[DataSourceMode] Route={Route} Mode=LiveAllowed Provider=Live",
            path);
        await _next(context);
    }
}
