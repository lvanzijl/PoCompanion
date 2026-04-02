using PoTool.Shared.DataState;

namespace PoTool.Api.Services;

public sealed class CacheStateResponseService
{
    private readonly CacheReadinessStateService _cacheReadinessStateService;
    private readonly ILogger<CacheStateResponseService> _logger;

    public CacheStateResponseService(
        CacheReadinessStateService cacheReadinessStateService,
        ILogger<CacheStateResponseService> logger)
    {
        _cacheReadinessStateService = cacheReadinessStateService;
        _logger = logger;
    }

    public async Task<DataStateResponseDto<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T?>> loadAsync,
        Func<T?, bool> isEmpty,
        string? emptyReason,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var readiness = await _cacheReadinessStateService.GetCurrentStateAsync(cancellationToken);
        if (readiness.State is DataStateDto.NotReady or DataStateDto.Failed)
        {
            return new DataStateResponseDto<T>
            {
                State = readiness.State,
                Reason = readiness.Reason,
                RetryAfterSeconds = readiness.RetryAfterSeconds
            };
        }

        try
        {
            var data = await loadAsync(cancellationToken);
            if (isEmpty(data))
            {
                return new DataStateResponseDto<T>
                {
                    State = DataStateDto.Empty,
                    Data = data,
                    Reason = emptyReason
                };
            }

            return new DataStateResponseDto<T>
            {
                State = DataStateDto.Available,
                Data = data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache state response failed for {FailureReason}", failureReason);
            return new DataStateResponseDto<T>
            {
                State = DataStateDto.Failed,
                Reason = failureReason
            };
        }
    }
}
