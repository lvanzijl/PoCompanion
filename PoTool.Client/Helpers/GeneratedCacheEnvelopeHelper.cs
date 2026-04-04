using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.DataState;

namespace PoTool.Client.Helpers;

public static class GeneratedCacheEnvelopeHelper
{
    public static CacheBackedClientResult<TData> ToCacheBackedResult<TData>(
        IGeneratedDataStateEnvelope<TData> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return CreateCacheBackedResult(envelope, static data => data);
    }

    public static CacheBackedClientResult<TMapped> ToCacheBackedResult<TData, TMapped>(
        IGeneratedDataStateEnvelope<TData> envelope,
        Func<TData, TMapped?> mapper)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(mapper);
        return CreateCacheBackedResult(envelope, mapper);
    }

    public static DataStateResponseDto<TData> ToDataStateResponse<TData>(
        IGeneratedDataStateEnvelope<TData> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return CreateDataStateResponse(envelope, static data => data);
    }

    public static DataStateResponseDto<TMapped> ToDataStateResponse<TData, TMapped>(
        IGeneratedDataStateEnvelope<TData> envelope,
        Func<TData, TMapped?> mapper)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(mapper);
        return CreateDataStateResponse(envelope, mapper);
    }

    public static TData GetDataOrDefault<TData>(
        IGeneratedDataStateEnvelope<TData> envelope,
        TData defaultValue)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var result = ToCacheBackedResult(envelope);
        return result.State == CacheBackedClientState.Success && result.Data is not null
            ? result.Data
            : defaultValue;
    }

    public static TData? GetDataOrDefault<TData>(IGeneratedDataStateEnvelope<TData> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var result = ToCacheBackedResult(envelope);
        return result.State == CacheBackedClientState.Success
            ? result.Data
            : default;
    }

    public static IReadOnlyList<TItem> GetDataOrDefault<TItem>(
        IGeneratedDataStateEnvelope<ICollection<TItem>> envelope,
        IReadOnlyCollection<TItem> defaultValue)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(defaultValue);

        var result = ToCacheBackedResult(
            envelope,
            static data => data.ToList());

        return result.State == CacheBackedClientState.Success && result.Data is not null
            ? result.Data
            : defaultValue.ToList();
    }

    public static IReadOnlyList<TItem> GetDataOrDefault<TItem>(
        IGeneratedDataStateEnvelope<ICollection<TItem>> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var result = ToCacheBackedResult(
            envelope,
            static data => data.ToList());

        return result.State == CacheBackedClientState.Success && result.Data is not null
            ? result.Data
            : Array.Empty<TItem>();
    }

    public static TMapped GetDataOrDefault<TData, TMapped>(
        IGeneratedDataStateEnvelope<TData> envelope,
        Func<TData, TMapped?> mapper,
        TMapped defaultValue)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(mapper);

        var result = ToCacheBackedResult(envelope, mapper);
        return result.State == CacheBackedClientState.Success && result.Data is not null
            ? result.Data
            : defaultValue;
    }

    public static TMapped? GetDataOrDefault<TData, TMapped>(
        IGeneratedDataStateEnvelope<TData> envelope,
        Func<TData, TMapped?> mapper)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(mapper);

        var result = ToCacheBackedResult(envelope, mapper);
        return result.State == CacheBackedClientState.Success
            ? result.Data
            : default;
    }

    private static CacheBackedClientResult<TMapped> CreateCacheBackedResult<TData, TMapped>(
        IGeneratedDataStateEnvelope<TData> envelope,
        Func<TData, TMapped?> mapper)
    {
        return envelope.State switch
        {
            DataStateDto.Available when envelope.Data is not null && mapper(envelope.Data) is { } mapped
                => CacheBackedClientResult<TMapped>.Success(mapped),
            DataStateDto.Available
                => CacheBackedClientResult<TMapped>.Failed(
                    "The generated cache-backed response did not contain usable data.",
                    envelope.RetryAfterSeconds),
            DataStateDto.Empty
                => CacheBackedClientResult<TMapped>.Empty(envelope.Reason, envelope.RetryAfterSeconds),
            DataStateDto.NotReady
                => CacheBackedClientResult<TMapped>.NotReady(envelope.Reason, envelope.RetryAfterSeconds),
            DataStateDto.Failed
                => CacheBackedClientResult<TMapped>.Failed(envelope.Reason, envelope.RetryAfterSeconds),
            _ => CacheBackedClientResult<TMapped>.Failed(
                "The generated cache-backed response did not contain usable data.",
                envelope.RetryAfterSeconds)
        };
    }

    private static DataStateResponseDto<TMapped> CreateDataStateResponse<TData, TMapped>(
        IGeneratedDataStateEnvelope<TData> envelope,
        Func<TData, TMapped?> mapper)
    {
        return new DataStateResponseDto<TMapped>
        {
            State = envelope.State,
            Data = envelope.Data is null ? default : mapper(envelope.Data),
            Reason = envelope.Reason,
            RetryAfterSeconds = envelope.RetryAfterSeconds
        };
    }
}
