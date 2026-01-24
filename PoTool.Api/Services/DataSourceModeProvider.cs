using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Configuration;

namespace PoTool.Api.Services;

/// <summary>
/// Provides the current data source mode.
/// Persists mode per product owner in the database.
/// </summary>
public sealed class DataSourceModeProvider : IDataSourceModeProvider
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<DataSourceModeProvider> _logger;

    // Thread-local current mode for in-request switching
    // Changed from defaulting to Live to nullable - mode MUST be explicitly set by middleware
    private DataSourceMode? _currentMode = null;

    public DataSourceModeProvider(
        PoToolDbContext dbContext,
        ILogger<DataSourceModeProvider> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current mode property (implements interface).
    /// Throws exception if mode has not been explicitly set by middleware.
    /// This enforces that all routes must have their mode set explicitly.
    /// </summary>
    public DataSourceMode Mode => _currentMode 
        ?? throw new InvalidOperationException(
            "DataSourceMode not set for this request. " +
            "Mode must be explicitly set by DataSourceModeMiddleware. " +
            "This indicates a middleware configuration issue.");

    /// <summary>
    /// Gets the data source mode for a product owner from the database.
    /// Falls back to Live mode if not set or no successful sync exists.
    /// </summary>
    public async Task<DataSourceMode> GetModeAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        // Check if there's a cache state for this product owner with a successful sync
        var cacheState = await _dbContext.ProductOwnerCacheStates
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.ProductOwnerId == productOwnerId, cancellationToken);

        if (cacheState == null)
        {
            _logger.LogDebug("No cache state found for ProductOwner {ProductOwnerId}, using Live mode", productOwnerId);
            _currentMode = DataSourceMode.Live;
            return DataSourceMode.Live;
        }

        // If we have a successful sync, use Cache mode
        if (cacheState.SyncStatus == CacheSyncStatus.Success && cacheState.LastSuccessfulSync.HasValue)
        {
            _logger.LogDebug("Cache available for ProductOwner {ProductOwnerId}, using Cache mode", productOwnerId);
            _currentMode = DataSourceMode.Cache;
            return DataSourceMode.Cache;
        }

        _logger.LogDebug("No successful sync for ProductOwner {ProductOwnerId}, using Live mode", productOwnerId);
        _currentMode = DataSourceMode.Live;
        return DataSourceMode.Live;
    }

    /// <summary>
    /// Sets the data source mode for a product owner.
    /// Mode is inferred from cache state, not explicitly stored.
    /// </summary>
    public async Task SetModeAsync(int productOwnerId, DataSourceMode mode, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting data source mode to {Mode} for ProductOwner {ProductOwnerId}", 
            mode, productOwnerId);

        // If setting to Cache, verify we have a valid cache
        if (mode == DataSourceMode.Cache)
        {
            var cacheState = await _dbContext.ProductOwnerCacheStates
                .FirstOrDefaultAsync(cs => cs.ProductOwnerId == productOwnerId, cancellationToken);

            if (cacheState == null || !cacheState.LastSuccessfulSync.HasValue)
            {
                _logger.LogWarning(
                    "Cannot set Cache mode for ProductOwner {ProductOwnerId}: no successful sync exists", 
                    productOwnerId);
                throw new InvalidOperationException("Cannot switch to Cache mode without a successful sync");
            }
        }

        _currentMode = mode;
    }

    /// <summary>
    /// Sets the current mode synchronously.
    /// </summary>
    public void SetCurrentMode(DataSourceMode mode)
    {
        _currentMode = mode;
    }
}
