using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Models;

public enum DataStateResultStatus
{
    NotRequested,
    Loading,
    Ready,
    Empty,
    NotReady,
    Failed,
    Invalid
}

/// <summary>
/// Canonical client-side result for cache-backed reads.
/// Preserves explicit data-state information and any canonical filter metadata.
/// </summary>
/// <typeparam name="T">Mapped payload type.</typeparam>
public sealed record DataStateResult<T>(
    DataStateResultStatus Status,
    DataStateDto DataState,
    T? Data = default,
    string? Reason = null,
    int? RetryAfterSeconds = null,
    IReadOnlyList<CanonicalFilterMetadata>? MetadataItems = null)
{
    private static readonly IReadOnlyList<CanonicalFilterMetadata> EmptyFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
    private static readonly IReadOnlyList<string> EmptyInvalidFields = Array.Empty<string>();
    private static readonly IReadOnlyList<FilterValidationIssueDto> EmptyValidationMessages = Array.Empty<FilterValidationIssueDto>();

    public DataStateDto State => DataState;

    public IReadOnlyList<CanonicalFilterMetadata> Metadata => MetadataItems ?? EmptyFilterMetadata;

    public CanonicalFilterMetadata? FilterMetadata => Metadata.Count == 1 ? Metadata[0] : null;

    public bool CanUseData => DataState == DataStateDto.Available && Data is not null;

    public bool HasInvalidFilter => InvalidFields.Count > 0;

    public object? RequestedFilter => Metadata.Count == 1 ? Metadata[0].RequestedFilter : null;

    public object? EffectiveFilter => Metadata.Count == 1 ? Metadata[0].EffectiveFilter : null;

    public IReadOnlyList<string> InvalidFields => Metadata.Count == 0
        ? EmptyInvalidFields
        : Metadata
            .SelectMany(item => item.InvalidFields)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<FilterValidationIssueDto> ValidationMessages => Metadata.Count == 0
        ? EmptyValidationMessages
        : Metadata
            .SelectMany(item => item.ValidationMessages)
            .GroupBy(message => $"{message.Field}\u001F{message.Message}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    public static DataStateResult<T> NotRequested(string? reason = null)
        => new(DataStateResultStatus.NotRequested, DataStateDto.NotRequested, Reason: reason);

    public static DataStateResult<T> Loading(string? reason = null)
        => new(DataStateResultStatus.Loading, DataStateDto.Loading, Reason: reason);

    public static DataStateResult<T> Ready(
        T data,
        IReadOnlyList<CanonicalFilterMetadata>? filterMetadata = null,
        string? reason = null,
        int? retryAfterSeconds = null)
        => new(
            DetermineStatus(DataStateDto.Available, filterMetadata),
            DataStateDto.Available,
            data,
            reason,
            retryAfterSeconds,
            filterMetadata);

    public static DataStateResult<T> Empty(
        string? reason = null,
        int? retryAfterSeconds = null,
        IReadOnlyList<CanonicalFilterMetadata>? filterMetadata = null)
        => new(
            DetermineStatus(DataStateDto.Empty, filterMetadata),
            DataStateDto.Empty,
            default,
            reason,
            retryAfterSeconds,
            filterMetadata);

    public static DataStateResult<T> NotReady(
        string? reason = null,
        int? retryAfterSeconds = null,
        IReadOnlyList<CanonicalFilterMetadata>? filterMetadata = null)
        => new(
            DetermineStatus(DataStateDto.NotReady, filterMetadata),
            DataStateDto.NotReady,
            default,
            reason,
            retryAfterSeconds,
            filterMetadata);

    public static DataStateResult<T> Failed(
        string? reason = null,
        int? retryAfterSeconds = null,
        IReadOnlyList<CanonicalFilterMetadata>? filterMetadata = null)
        => new(
            DetermineStatus(DataStateDto.Failed, filterMetadata),
            DataStateDto.Failed,
            default,
            reason,
            retryAfterSeconds,
            filterMetadata);

    public static DataStateResult<T> Invalid(
        string? reason = null,
        T? data = default,
        int? retryAfterSeconds = null,
        IReadOnlyList<CanonicalFilterMetadata>? filterMetadata = null)
        // Invalid means the requested filter scope needs attention. Some callers still receive corrected data,
        // so the underlying DataState stays Available when data exists and Failed when nothing usable remains.
        => new(DataStateResultStatus.Invalid, data is null ? DataStateDto.Failed : DataStateDto.Available, data, reason, retryAfterSeconds, filterMetadata);

    public DataStateResult<TMapped> Map<TMapped>(Func<T, TMapped> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return CanUseData
            ? new DataStateResult<TMapped>(Status, DataState, map(Data!), Reason, RetryAfterSeconds, Metadata)
            : new DataStateResult<TMapped>(Status, DataState, default, Reason, RetryAfterSeconds, Metadata);
    }

    private static DataStateResultStatus DetermineStatus(
        DataStateDto dataState,
        IReadOnlyList<CanonicalFilterMetadata>? filterMetadata)
    {
        if (filterMetadata is { Count: > 0 } && filterMetadata.Any(item => item.InvalidFields.Count > 0))
        {
            return DataStateResultStatus.Invalid;
        }

        return dataState switch
        {
            DataStateDto.Available => DataStateResultStatus.Ready,
            DataStateDto.Empty => DataStateResultStatus.Empty,
            DataStateDto.NotReady => DataStateResultStatus.NotReady,
            DataStateDto.Failed => DataStateResultStatus.Failed,
            DataStateDto.Loading => DataStateResultStatus.Loading,
            _ => DataStateResultStatus.NotRequested
        };
    }
}
