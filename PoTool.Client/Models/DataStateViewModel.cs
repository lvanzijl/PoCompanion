using PoTool.Shared.DataState;

namespace PoTool.Client.Models;

public sealed record DataStateViewModel<T>(
    DataStateDto State,
    T? Data = default,
    string? Reason = null,
    int? RetryAfterSeconds = null)
{
    public static DataStateViewModel<T> NotRequested()
        => new(DataStateDto.NotRequested);

    public static DataStateViewModel<T> Loading()
        => new(DataStateDto.Loading);

    public static DataStateViewModel<T> FromResponse(DataStateResponseDto<T>? response, string failureReason)
    {
        if (response is null)
        {
            return new DataStateViewModel<T>(DataStateDto.Failed, Reason: failureReason);
        }

        return new DataStateViewModel<T>(response.State, response.Data, response.Reason, response.RetryAfterSeconds);
    }
}
