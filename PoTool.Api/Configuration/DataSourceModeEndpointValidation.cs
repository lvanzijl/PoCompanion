using Microsoft.AspNetCore.Http;

namespace PoTool.Api.Configuration;

public static class DataSourceModeEndpointValidation
{
    public static void ValidateManagedEndpoints(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var endpoints = services
            .GetServices<EndpointDataSource>()
            .SelectMany(dataSource => dataSource.Endpoints);

        ValidateManagedEndpoints(endpoints);
    }

    public static void ValidateManagedEndpoints(IEnumerable<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var violations = endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Endpoint = endpoint,
                Path = NormalizePath(endpoint.RoutePattern.RawText)
            })
            .Where(entry => !DataSourceModeConfiguration.ShouldBypassMiddleware(entry.Path))
            .Select(entry => ValidateEndpoint(entry.Endpoint, entry.Path))
            .Where(message => message is not null)
            .ToArray();

        if (violations.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Managed endpoints must declare exactly one data-source classification metadata entry at definition time:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(message => $"- {message}")));
    }

    private static string? ValidateEndpoint(RouteEndpoint endpoint, string? path)
    {
        var metadata = endpoint.Metadata.GetOrderedMetadata<IDataSourceModeMetadata>().ToArray();
        if (metadata.Length == 0)
        {
            return $"{path ?? endpoint.DisplayName ?? "<unknown>"} is missing DataSourceMode metadata.";
        }

        if (metadata.Length > 1)
        {
            return $"{path ?? endpoint.DisplayName ?? "<unknown>"} has multiple DataSourceMode metadata entries.";
        }

        var fallbackIntent = DataSourceModeConfiguration.GetRouteIntent(path);
        if (fallbackIntent is not RouteIntent.Unknown &&
            fallbackIntent != metadata[0].RouteIntent)
        {
            return $"{path ?? endpoint.DisplayName ?? "<unknown>"} metadata {metadata[0].RouteIntent} does not match fallback classification {fallbackIntent}.";
        }

        return null;
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var normalized = path.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : normalized;
    }
}
