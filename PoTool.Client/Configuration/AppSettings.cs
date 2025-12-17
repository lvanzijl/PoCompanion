namespace PoTool.Client.Configuration;

/// <summary>
/// Application settings and configuration values.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The default area path for TFS/Azure DevOps work item queries.
    /// </summary>
    public const string DefaultAreaPath = "DefaultAreaPath";

    /// <summary>
    /// The local storage key for saving expanded tree node state.
    /// </summary>
    public const string TreeExpandedStateKey = "workitem-tree-expanded";

    /// <summary>
    /// Default timeout for HTTP requests in seconds.
    /// </summary>
    public const int DefaultHttpTimeoutSeconds = 30;

    /// <summary>
    /// Delay in milliseconds after requesting sync via SignalR before reloading data.
    /// </summary>
    public const int SyncDelayMilliseconds = 300;
}
