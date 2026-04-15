using PoTool.Client.Models;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Helpers;

public static class DataStateResultFactory
{
    public static DataStateResult<TData> ToDataStateResult<TData>(this DataStateResponseDto<TData>? response)
    {
        if (response is null)
        {
            return DataStateResult<TData>.Failed("The cache-backed response was missing.");
        }

        return response.State switch
        {
            DataStateDto.Available when response.Data is not null => DataStateResult<TData>.Ready(
                response.Data,
                reason: response.Reason,
                retryAfterSeconds: response.RetryAfterSeconds),
            DataStateDto.Empty => DataStateResult<TData>.Empty(response.Reason, response.RetryAfterSeconds),
            DataStateDto.NotReady => DataStateResult<TData>.NotReady(response.Reason, response.RetryAfterSeconds),
            DataStateDto.Failed => DataStateResult<TData>.Failed(response.Reason, response.RetryAfterSeconds),
            DataStateDto.Loading => DataStateResult<TData>.Loading(response.Reason),
            _ => DataStateResult<TData>.Failed(response.Reason ?? "The cache-backed response did not contain usable data.", response.RetryAfterSeconds)
        };
    }

    public static DataStateResult<TData> ToDataStateResult<TData>(this DataStateResponseDto<DeliveryQueryResponseDto<TData>>? response)
        => ToCanonicalDataStateResult(
            response,
            static data => data.Data,
            static data => CanonicalClientResponseFactory.Create(data).FilterMetadata);

    public static DataStateResult<TData> ToDataStateResult<TData>(this DataStateResponseDto<SprintQueryResponseDto<TData>>? response)
        => ToCanonicalDataStateResult(
            response,
            static data => data.Data,
            static data => CanonicalClientResponseFactory.Create(data).FilterMetadata);

    public static DataStateResult<TData> ToDataStateResult<TData>(this DataStateResponseDto<PipelineQueryResponseDto<TData>>? response)
        => ToCanonicalDataStateResult(
            response,
            static data => data.Data,
            static data => CanonicalClientResponseFactory.Create(data).FilterMetadata);

    public static DataStateResult<TData> ToDataStateResult<TData>(this DataStateResponseDto<PullRequestQueryResponseDto<TData>>? response)
        => ToCanonicalDataStateResult(
            response,
            static data => data.Data,
            static data => CanonicalClientResponseFactory.Create(data).FilterMetadata);

    private static DataStateResult<TData> ToCanonicalDataStateResult<TEnvelope, TData>(
        DataStateResponseDto<TEnvelope>? response,
        Func<TEnvelope, TData?> dataSelector,
        Func<TEnvelope, CanonicalFilterMetadata?> filterMetadataSelector)
    {
        if (response is null)
        {
            return DataStateResult<TData>.Failed("The cache-backed response was missing.");
        }

        var metadata = response.Data is null
            ? Array.Empty<CanonicalFilterMetadata>()
            : ToMetadataList(filterMetadataSelector(response.Data));

        return response.State switch
        {
            DataStateDto.Available when response.Data is not null && dataSelector(response.Data) is { } data => DataStateResult<TData>.Ready(
                data,
                metadata,
                response.Reason,
                response.RetryAfterSeconds),
            DataStateDto.Empty => DataStateResult<TData>.Empty(response.Reason, response.RetryAfterSeconds, metadata),
            DataStateDto.NotReady => DataStateResult<TData>.NotReady(response.Reason, response.RetryAfterSeconds, metadata),
            DataStateDto.Failed => DataStateResult<TData>.Failed(response.Reason, response.RetryAfterSeconds, metadata),
            DataStateDto.Loading => DataStateResult<TData>.Loading(response.Reason),
            _ => DataStateResult<TData>.Failed(
                response.Reason ?? "The cache-backed response did not contain usable data.",
                response.RetryAfterSeconds,
                metadata)
        };
    }

    private static IReadOnlyList<CanonicalFilterMetadata> ToMetadataList(CanonicalFilterMetadata? metadata)
        => metadata is null ? Array.Empty<CanonicalFilterMetadata>() : [metadata];
}
