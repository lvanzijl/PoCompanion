using PoTool.Core.Contracts;
using PoTool.Shared.DataState;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

public sealed record CacheReadinessState(
    DataStateDto State,
    string? Reason = null,
    int? RetryAfterSeconds = null,
    int? ProductOwnerId = null);

public sealed class CacheReadinessStateService
{
    private const int DefaultRetryAfterSeconds = 2;

    private readonly ICurrentProfileProvider _currentProfileProvider;
    private readonly ICacheStateRepository _cacheStateRepository;

    public CacheReadinessStateService(
        ICurrentProfileProvider currentProfileProvider,
        ICacheStateRepository cacheStateRepository)
    {
        _currentProfileProvider = currentProfileProvider;
        _cacheStateRepository = cacheStateRepository;
    }

    public async Task<CacheReadinessState> GetCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var productOwnerId = await _currentProfileProvider.GetCurrentProductOwnerIdAsync(cancellationToken);
        if (!productOwnerId.HasValue)
        {
            return new CacheReadinessState(
                DataStateDto.NotReady,
                "Select an active profile before loading cached workspace data.");
        }

        var cacheState = await _cacheStateRepository.GetCacheStateAsync(productOwnerId.Value, cancellationToken);
        if (cacheState is null)
        {
            return new CacheReadinessState(
                DataStateDto.NotReady,
                "Cache has not been built for the active profile yet.",
                DefaultRetryAfterSeconds,
                productOwnerId.Value);
        }

        if (cacheState.LastSuccessfulSync.HasValue)
        {
            return new CacheReadinessState(DataStateDto.Available, ProductOwnerId: productOwnerId.Value);
        }

        return cacheState.SyncStatus switch
        {
            CacheSyncStatusDto.InProgress => new CacheReadinessState(
                DataStateDto.NotReady,
                string.IsNullOrWhiteSpace(cacheState.CurrentSyncStage)
                    ? "Cache is warming for the active profile."
                    : $"Cache is warming: {cacheState.CurrentSyncStage}.",
                DefaultRetryAfterSeconds,
                productOwnerId.Value),
            CacheSyncStatusDto.Failed => new CacheReadinessState(
                DataStateDto.Failed,
                string.IsNullOrWhiteSpace(cacheState.LastErrorMessage)
                    ? "The latest cache sync failed."
                    : cacheState.LastErrorMessage,
                ProductOwnerId: productOwnerId.Value),
            _ => new CacheReadinessState(
                DataStateDto.NotReady,
                "Cache has not been built for the active profile yet.",
                DefaultRetryAfterSeconds,
                productOwnerId.Value)
        };
    }
}
