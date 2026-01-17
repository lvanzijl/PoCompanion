namespace PoTool.Core.Configuration;

/// <summary>
/// Defines the data source mode for the application.
/// Live mode is the only supported mode - data is fetched directly from TFS/Azure DevOps.
/// </summary>
public enum DataSourceMode
{
    /// <summary>
    /// Live mode: Direct calls to TFS APIs. No cache interaction.
    /// This is the only supported mode.
    /// </summary>
    Live = 0
}
