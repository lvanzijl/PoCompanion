using Microsoft.AspNetCore.Builder;

namespace PoTool.Api.Configuration;

public static class DataSourceModeEndpointConventionBuilderExtensions
{
    public static TBuilder WithDataSourceMode<TBuilder>(this TBuilder builder, RouteIntent routeIntent)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(new DataSourceModeMetadata(routeIntent));
        return builder;
    }
}
