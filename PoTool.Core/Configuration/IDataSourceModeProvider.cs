namespace PoTool.Core.Configuration;

/// <summary>
/// Provides the active data source mode for the application.
/// This is the single source of truth for determining whether to use Live or Cached data.
/// </summary>
public interface IDataSourceModeProvider
{
    /// <summary>
    /// Gets the currently configured data source mode.
    /// </summary>
    DataSourceMode Mode { get; }
}
