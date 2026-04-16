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
        return TryGetRouteIntent(context.GetEndpoint()?.Metadata, out var routeIntent)
            ? routeIntent
            : DataSourceModeConfiguration.ResolveRouteIntentOrThrow(path);
    }

    public static bool RequiresCache(Endpoint? endpoint, string? path = null)
        => ResolveRouteIntent(endpoint?.Metadata, path) == RouteIntent.CacheOnlyAnalyticalRead;

    public static bool RequiresCache(ActionDescriptor actionDescriptor, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(actionDescriptor);

        return ResolveRouteIntent(actionDescriptor.EndpointMetadata, path) == RouteIntent.CacheOnlyAnalyticalRead;
    }

    public static bool RequiresCache(MethodInfo methodInfo, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        return ResolveDeclaredRouteIntent(methodInfo) == RouteIntent.CacheOnlyAnalyticalRead
            || ResolveRouteIntent(path) == RouteIntent.CacheOnlyAnalyticalRead;
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
        if (metadata is not null)
        {
            var classification = metadata.GetMetadata<IDataSourceModeMetadata>();
            if (classification is not null)
            {
                routeIntent = classification.RouteIntent;
                return true;
            }
        }

        routeIntent = RouteIntent.Unknown;
        return false;
    }

    private static RouteIntent ResolveRouteIntent(IList<object>? metadata, string? path)
    {
        if (metadata is not null)
        {
            var classification = metadata.OfType<IDataSourceModeMetadata>().LastOrDefault();
            if (classification is not null)
            {
                return classification.RouteIntent;
            }
        }

        return ResolveRouteIntent(path);
    }

    private static RouteIntent ResolveRouteIntent(string? path)
        => path is null
            ? RouteIntent.Unknown
            : DataSourceModeConfiguration.GetRouteIntent(path);
}
