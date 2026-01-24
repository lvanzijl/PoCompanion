namespace PoTool.Core.Configuration;

/// <summary>
/// Defines the data source mode for the application.
/// </summary>
public enum DataSourceMode
{
    /// <summary>
    /// Live mode: Direct calls to TFS APIs. No cache interaction.
    /// Higher latency but always up-to-date.
    /// </summary>
    Live = 0,

    /// <summary>
    /// Cache mode: Data is read from local SQLite cache.
    /// Lower latency but requires sync to be up-to-date.
    /// </summary>
    Cache = 1
}
