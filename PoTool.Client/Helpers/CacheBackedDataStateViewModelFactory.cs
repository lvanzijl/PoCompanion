using PoTool.Client.Models;

namespace PoTool.Client.Helpers;

public static class CacheBackedDataStateViewModelFactory
{
    public static DataStateViewModel<TData> ToViewModel<TData>(
        this CacheBackedClientResult<TData>? result,
        string missingResponseReason)
    {
        if (result is null)
        {
            return DataStateViewModel<TData>.Failed(missingResponseReason);
        }

        return result.State switch
        {
            CacheBackedClientState.Success when result.Data is not null => DataStateViewModel<TData>.Ready(result.Data),
            CacheBackedClientState.Empty => DataStateViewModel<TData>.Empty(result.Reason),
            CacheBackedClientState.NotReady => new DataStateViewModel<TData>(
                PoTool.Shared.DataState.DataStateDto.NotReady,
                Reason: result.Reason,
                RetryAfterSeconds: result.RetryAfterSeconds,
                ResultStatus: DataStateResultStatus.NotReady),
            CacheBackedClientState.Failed or CacheBackedClientState.Unavailable => DataStateViewModel<TData>.Failed(result.Reason ?? missingResponseReason),
            _ => DataStateViewModel<TData>.Failed(result.Reason ?? missingResponseReason)
        };
    }

    public static DataStateViewModel<TData> ToViewModel<TData>(
        this CacheBackedClientResult<CanonicalClientResponse<TData>>? result,
        string missingResponseReason)
    {
        if (result is null)
        {
            return DataStateViewModel<TData>.Failed(missingResponseReason);
        }

        return result.State switch
        {
            CacheBackedClientState.Success when result.Data is not null => CreateCanonicalReady(result.Data),
            CacheBackedClientState.Empty => DataStateViewModel<TData>.Empty(result.Reason),
            CacheBackedClientState.NotReady => new DataStateViewModel<TData>(
                PoTool.Shared.DataState.DataStateDto.NotReady,
                Reason: result.Reason,
                RetryAfterSeconds: result.RetryAfterSeconds,
                ResultStatus: DataStateResultStatus.NotReady),
            CacheBackedClientState.Failed or CacheBackedClientState.Unavailable => DataStateViewModel<TData>.Failed(result.Reason ?? missingResponseReason),
            _ => DataStateViewModel<TData>.Failed(result.Reason ?? missingResponseReason)
        };
    }

    private static DataStateViewModel<TData> CreateCanonicalReady<TData>(CanonicalClientResponse<TData> response)
    {
        var metadata = response.FilterMetadata;
        return metadata is { HasInvalidFields: true }
            ? DataStateViewModel<TData>.Invalid(
                reason: metadata.ValidationMessages.FirstOrDefault()?.Message ?? "The current filter selection cannot be resolved for this view.",
                data: response.Data,
                invalidFields: metadata.InvalidFields,
                validationMessages: metadata.ValidationMessages,
                filterMetadata: metadata)
            : DataStateViewModel<TData>.Ready(response.Data) with
            {
                FilterMetadata = metadata,
                InvalidFields = metadata?.InvalidFields ?? Array.Empty<string>(),
                ValidationMessages = metadata?.ValidationMessages ?? Array.Empty<PoTool.Shared.Metrics.FilterValidationIssueDto>()
            };
    }
}
