using PoTool.Client.Helpers;
using PoTool.Shared.DataState;

namespace PoTool.Client.Models;

public sealed record DataStateViewModel<T>(
    DataStateDto State,
    T? Data = default,
    string? Reason = null,
    int? RetryAfterSeconds = null)
{
    public UiDataState UiState => CacheStatePresentation.ToUiDataState(State);

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
            return Failed(failureReason);
        }

        return new DataStateViewModel<T>(response.State, response.Data, response.Reason, response.RetryAfterSeconds);
    }
}
