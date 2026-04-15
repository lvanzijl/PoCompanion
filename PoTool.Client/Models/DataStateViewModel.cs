using System.Collections.Concurrent;
using System.Reflection;
using PoTool.Client.Helpers;
using PoTool.Shared.DataState;

namespace PoTool.Client.Models;

public sealed record DataStateViewModel<T>(
    DataStateDto State,
    T? Data = default,
    string? Reason = null,
    int? RetryAfterSeconds = null,
    DataStateResultStatus? ResultStatus = null)
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> InvalidFieldsPropertyCache = new();

    public UiDataState UiState => ResultStatus.HasValue
        ? CacheStatePresentation.ToUiDataState(ResultStatus.Value)
        : CacheStatePresentation.ToUiDataState(State);

    public static DataStateViewModel<T> NotRequested()
        => new(DataStateDto.NotRequested);

    public static DataStateViewModel<T> Loading()
        => new(DataStateDto.Loading);

    public static DataStateViewModel<T> Ready(T data)
        => new(DataStateDto.Available, data);

    public static DataStateViewModel<T> Empty(string? reason = null)
        => new(DataStateDto.Empty, Reason: reason);

    public static DataStateViewModel<T> Failed(string? reason = null)
        => new(DataStateDto.Failed, Reason: reason);

    public static DataStateViewModel<T> FromResponse(DataStateResponseDto<T>? response, string failureReason)
    {
        if (response is null)
        {
            return new DataStateViewModel<T>(DataStateDto.Failed, Reason: failureReason, ResultStatus: DataStateResultStatus.Failed);
        }

        return new DataStateViewModel<T>(
            response.State,
            response.Data,
            response.Reason,
            response.RetryAfterSeconds,
            DetermineResultStatus(response));
    }

    public static DataStateViewModel<T> FromResult(DataStateResult<T> result)
        => new(result.State, result.Data, result.Reason, result.RetryAfterSeconds, result.Status);

    private static DataStateResultStatus DetermineResultStatus(DataStateResponseDto<T> response)
    {
        if (response.State == DataStateDto.Available && HasInvalidFilter(response.Data))
        {
            return DataStateResultStatus.Invalid;
        }

        return response.State switch
        {
            DataStateDto.Available => DataStateResultStatus.Ready,
            DataStateDto.Empty => DataStateResultStatus.Empty,
            DataStateDto.NotReady => DataStateResultStatus.NotReady,
            DataStateDto.Failed => DataStateResultStatus.Failed,
            DataStateDto.Loading => DataStateResultStatus.Loading,
            _ => DataStateResultStatus.NotRequested
        };
    }

    private static bool HasInvalidFilter(T? data)
    {
        if (data is null)
        {
            return false;
        }

        var invalidFieldsProperty = InvalidFieldsPropertyCache.GetOrAdd(
            data.GetType(),
            static type => type.GetProperty("InvalidFields"));
        if (invalidFieldsProperty?.GetValue(data) is System.Collections.IEnumerable invalidFields)
        {
            foreach (var _ in invalidFields)
            {
                return true;
            }
        }

        return false;
    }
}
