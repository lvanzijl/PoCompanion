namespace PoTool.Api.Configuration;

public interface IDataSourceModeMetadata
{
    RouteIntent RouteIntent { get; }
}

public sealed record DataSourceModeMetadata(RouteIntent RouteIntent) : IDataSourceModeMetadata;
