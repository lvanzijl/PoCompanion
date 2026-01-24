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

    /// <summary>
    /// Gets the current data source mode for a specific product owner.
    /// </summary>
    /// <param name="productOwnerId">The product owner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current data source mode.</returns>
    Task<DataSourceMode> GetModeAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the data source mode for a specific product owner.
    /// </summary>
    /// <param name="productOwnerId">The product owner ID.</param>
    /// <param name="mode">The data source mode to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetModeAsync(int productOwnerId, DataSourceMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the current mode synchronously (for in-request switching).
    /// </summary>
    /// <param name="mode">The data source mode to set.</param>
    void SetCurrentMode(DataSourceMode mode);
}
