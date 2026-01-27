using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Interface for TFS configuration persistence.
/// Provides access to TFS connection settings stored in the database.
/// </summary>
public interface ITfsConfigurationService
{
    /// <summary>
    /// Gets the TFS configuration entity from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The TFS configuration entity, or null if not configured.</returns>
    Task<TfsConfigEntity?> GetConfigEntityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the TFS configuration entity to persistent storage.
    /// </summary>
    /// <param name="entity">The TFS configuration entity to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveConfigEntityAsync(TfsConfigEntity entity, CancellationToken cancellationToken = default);
}
