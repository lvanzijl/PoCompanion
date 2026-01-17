namespace PoTool.Core.Configuration;

/// <summary>
/// Defines the data source mode for the application.
/// </summary>
public enum DataSourceMode
{
    /// <summary>
    /// Live mode: Direct calls to TFS APIs. No cache interaction.
    /// </summary>
    Live = 0,

    /// <summary>
    /// Cached mode: Uses SQLite/EF-based cache.
    /// </summary>
    Cached = 1
}
