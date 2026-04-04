using PoTool.Shared.DataState;

namespace PoTool.Client.Models;

/// <summary>
/// Client-side interpretation of a cache-backed envelope, including transport failures.
/// </summary>
public enum CacheBackedClientState
{
    Success,
    Empty,
    NotReady,
    Failed,
    Unavailable
}

/// <summary>
/// Structured result for cache-backed client reads.
/// </summary>
public sealed record CacheBackedClientResult<T>(
    CacheBackedClientState State,
    T? Data = default,
    string? Reason = null,
    int? RetryAfterSeconds = null)
{
    public static CacheBackedClientResult<T> Success(T data)
        => new(CacheBackedClientState.Success, data);

    public static CacheBackedClientResult<T> Empty(string? reason = null, int? retryAfterSeconds = null)
        => new(CacheBackedClientState.Empty, default, reason, retryAfterSeconds);

    public static CacheBackedClientResult<T> NotReady(string? reason = null, int? retryAfterSeconds = null)
        => new(CacheBackedClientState.NotReady, default, reason, retryAfterSeconds);

    public static CacheBackedClientResult<T> Failed(string? reason = null, int? retryAfterSeconds = null)
        => new(CacheBackedClientState.Failed, default, reason, retryAfterSeconds);

    public static CacheBackedClientResult<T> Unavailable(string? reason = null, int? retryAfterSeconds = null)
        => new(CacheBackedClientState.Unavailable, default, reason, retryAfterSeconds);

    public DataStateDto ToDataStateDto()
        => State switch
        {
            CacheBackedClientState.Success => DataStateDto.Available,
            CacheBackedClientState.Empty => DataStateDto.Empty,
            CacheBackedClientState.NotReady => DataStateDto.NotReady,
            CacheBackedClientState.Failed => DataStateDto.Failed,
            CacheBackedClientState.Unavailable => DataStateDto.Failed,
            _ => DataStateDto.Failed
        };

    public CacheBackedClientResult<TMapped> Map<TMapped>(Func<T, TMapped> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return State switch
        {
            CacheBackedClientState.Success when Data is not null => CacheBackedClientResult<TMapped>.Success(map(Data)),
            CacheBackedClientState.Empty => CacheBackedClientResult<TMapped>.Empty(Reason, RetryAfterSeconds),
            CacheBackedClientState.NotReady => CacheBackedClientResult<TMapped>.NotReady(Reason, RetryAfterSeconds),
            CacheBackedClientState.Failed => CacheBackedClientResult<TMapped>.Failed(Reason, RetryAfterSeconds),
            CacheBackedClientState.Unavailable => CacheBackedClientResult<TMapped>.Unavailable(Reason, RetryAfterSeconds),
            _ => CacheBackedClientResult<TMapped>.Failed("The cache-backed response did not contain valid data.", RetryAfterSeconds)
        };
    }
}
