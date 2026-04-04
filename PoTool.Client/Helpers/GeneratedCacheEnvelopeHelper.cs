using System.Reflection;
using System.Text.Json;
using PoTool.Client.Models;
using PoTool.Shared.DataState;

namespace PoTool.Client.Helpers;

public static class GeneratedCacheEnvelopeHelper
{
    private static readonly JsonSerializerOptions SerializationOptions = JsonHelper.CaseInsensitiveOptions;

    public static CacheBackedClientResult<TData> ToCacheBackedResult<TEnvelope, TData>(
        TEnvelope envelope,
        Func<TEnvelope, TData?> dataSelector)
        where TEnvelope : class
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(dataSelector);

        var state = GetRequiredValue<DataStateDto>(envelope, "State");
        var data = dataSelector(envelope);
        var reason = GetOptionalValue<string>(envelope, "Reason");
        var retryAfterSeconds = GetOptionalValue<int?>(envelope, "RetryAfterSeconds");

        return state switch
        {
            DataStateDto.Available when data is not null => CacheBackedClientResult<TData>.Success(data),
            DataStateDto.Empty => CacheBackedClientResult<TData>.Empty(reason, retryAfterSeconds),
            DataStateDto.NotReady => CacheBackedClientResult<TData>.NotReady(reason, retryAfterSeconds),
            DataStateDto.Failed => CacheBackedClientResult<TData>.Failed(reason, retryAfterSeconds),
            _ => CacheBackedClientResult<TData>.Failed("The generated cache-backed response did not contain usable data.", retryAfterSeconds)
        };
    }

    public static CacheBackedClientResult<TData> ToCacheBackedResult<TData>(object envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return ToCacheBackedResult(envelope, current => GetOptionalValue<TData>(current, "Data"));
    }

    public static DataStateResponseDto<TData> ToDataStateResponse<TData>(object envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new DataStateResponseDto<TData>
        {
            State = GetRequiredValue<DataStateDto>(envelope, "State"),
            Data = GetOptionalValue<TData>(envelope, "Data"),
            Reason = GetOptionalValue<string>(envelope, "Reason"),
            RetryAfterSeconds = GetOptionalValue<int?>(envelope, "RetryAfterSeconds")
        };
    }

    public static TData GetDataOrDefault<TData>(
        object envelope,
        TData defaultValue)
    {
        if (envelope is null)
        {
            return defaultValue;
        }

        var result = ToCacheBackedResult(envelope, current => GetOptionalValue<TData>(current, "Data"));
        return result.State == CacheBackedClientState.Success && result.Data is not null
            ? result.Data
            : defaultValue;
    }

    public static TData? GetDataOrDefault<TData>(object envelope)
    {
        if (envelope is null)
        {
            return default;
        }

        var result = ToCacheBackedResult(envelope, current => GetOptionalValue<TData>(current, "Data"));
        return result.State == CacheBackedClientState.Success
            ? result.Data
            : default;
    }

    private static TValue GetRequiredValue<TValue>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Generated cache-backed response type '{instance.GetType().FullName}' is missing property '{propertyName}'.");

        var value = property.GetValue(instance);
        if (value is null)
        {
            throw new InvalidOperationException($"Generated cache-backed response type '{instance.GetType().FullName}' returned null for required property '{propertyName}'.");
        }

        return (TValue)value;
    }

    private static TValue? GetOptionalValue<TValue>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Generated cache-backed response type '{instance.GetType().FullName}' is missing property '{propertyName}'.");

        var value = property.GetValue(instance);
        return ConvertValue<TValue>(value);
    }

    private static TValue? ConvertValue<TValue>(object? value)
    {
        if (value is null)
        {
            return default;
        }

        if (value is TValue typedValue)
        {
            return typedValue;
        }

        var json = JsonSerializer.Serialize(value, SerializationOptions);
        return JsonSerializer.Deserialize<TValue>(json, SerializationOptions);
    }
}
