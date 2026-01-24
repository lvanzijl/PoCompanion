namespace PoTool.Client.Configuration;

/// <summary>
/// Application settings and configuration values.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The local storage key for saving expanded tree node state.
    /// </summary>
    public const string TreeExpandedStateKey = "workitem-tree-expanded";

    /// <summary>
    /// Default timeout for HTTP requests in seconds.
    /// </summary>
    public const int DefaultHttpTimeoutSeconds = 30;
}
