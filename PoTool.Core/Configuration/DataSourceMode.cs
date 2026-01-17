namespace PoTool.Core.Configuration;

/// <summary>
/// Defines the data source mode for the application.
/// </summary>
public enum DataSourceMode
{
    /// <summary>
    /// Live mode: Direct calls to TFS APIs. No cache interaction.
    /// This is the default and recommended mode.
    /// </summary>
    Live = 0,

    /// <summary>
    /// Cached mode: Uses SQLite/EF-based cache.
    /// DEPRECATED: This mode is maintained for backward compatibility only.
    /// Live mode is the default and recommended for all new deployments.
    /// </summary>
    [Obsolete("Cached mode is deprecated. Use Live mode for cache-free operation.", false)]
    Cached = 1
}
