using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;

namespace PoTool.Api.Configuration;

public static class DataSourceModeEndpointMetadataResolver
{
    public static RouteIntent ResolveRouteIntentOrThrow(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = context.Request.Path.Value;
        var routeIntent = ResolveMetadataRouteIntent(context.GetEndpoint()?.Metadata, path);
        return routeIntent != RouteIntent.Unknown
            ? routeIntent
            : DataSourceModeConfiguration.ResolveRouteIntentOrThrow(path);
    }

    public static bool RequiresCache(Endpoint? endpoint, string? path = null)
        => ResolveRouteIntent(endpoint?.Metadata?.OfType<object>().ToArray(), path) == RouteIntent.CacheOnlyAnalyticalRead;

    public static bool RequiresCache(ActionDescriptor actionDescriptor, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(actionDescriptor);

        return ResolveRouteIntent(actionDescriptor.EndpointMetadata, path) == RouteIntent.CacheOnlyAnalyticalRead;
    }

    public static bool RequiresCache(MethodInfo methodInfo, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        var declaredRouteIntent = ResolveDeclaredRouteIntent(methodInfo);
        if (declaredRouteIntent != RouteIntent.Unknown)
        {
            return declaredRouteIntent == RouteIntent.CacheOnlyAnalyticalRead;
        }

        return ResolveRouteIntent(path) == RouteIntent.CacheOnlyAnalyticalRead;
    }

    public static RouteIntent ResolveDeclaredRouteIntent(MethodInfo methodInfo)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        return methodInfo.GetCustomAttribute<DataSourceModeAttribute>(inherit: true)?.RouteIntent
            ?? methodInfo.DeclaringType?.GetCustomAttribute<DataSourceModeAttribute>(inherit: true)?.RouteIntent
            ?? RouteIntent.Unknown;
    }

    public static bool TryGetRouteIntent(EndpointMetadataCollection? metadata, out RouteIntent routeIntent)
    {
        routeIntent = ResolveMetadataRouteIntent(metadata, path: null);
        return routeIntent != RouteIntent.Unknown;
    }

    private static RouteIntent ResolveRouteIntent(IList<object>? metadata, string? path)
    {
        if (metadata is not null)
        {
            return ResolveMetadataRouteIntent(metadata, path);
        }

        return ResolveRouteIntent(path);
    }

    private static RouteIntent ResolveRouteIntent(string? path)
        => path is null
            ? RouteIntent.Unknown
            : DataSourceModeConfiguration.GetRouteIntent(path);

    private static RouteIntent ResolveMetadataRouteIntent(IEnumerable<object>? metadata, string? path)
    {
        var classifications = metadata?.OfType<IDataSourceModeMetadata>().ToArray() ?? [];
        return classifications.Length switch
        {
            0 => RouteIntent.Unknown,
            1 => classifications[0].RouteIntent,
            _ => throw new InvalidOperationException($"Endpoint '{path ?? "<unknown>"}' has multiple DataSourceMode metadata entries.")
        };
    }
}
