namespace PoTool.Api.Exceptions;

/// <summary>
/// Thrown when a request route has not been explicitly classified for data source enforcement.
/// </summary>
public sealed class RouteNotClassifiedException : InvalidOperationException
{
    public RouteNotClassifiedException(string? path)
        : base($"Route '{path ?? "<null>"}' is not classified by endpoint metadata or the DataSourceModeConfiguration fallback. Unknown fallback is disabled.")
    {
        Path = path;
    }

    public string? Path { get; }
}
