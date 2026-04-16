using PoTool.Client.Helpers;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Models;

public sealed record DataStateViewModel<T>(
    DataStateDto State,
    T? Data = default,
    string? Reason = null,
    int? RetryAfterSeconds = null,
    DataStateResultStatus? ResultStatus = null)
{
    private static readonly IReadOnlyList<string> EmptyInvalidFields = Array.Empty<string>();
    private static readonly IReadOnlyList<FilterValidationIssueDto> EmptyValidationMessages = Array.Empty<FilterValidationIssueDto>();

    public UiDataState UiState => ResultStatus.HasValue
        ? CacheStatePresentation.ToUiDataState(ResultStatus.Value)
        : CacheStatePresentation.ToUiDataState(State);

    public IReadOnlyList<string> InvalidFields { get; init; } = EmptyInvalidFields;

    public IReadOnlyList<FilterValidationIssueDto> ValidationMessages { get; init; } = EmptyValidationMessages;

    public CanonicalFilterMetadata? FilterMetadata { get; init; }

    public bool ShowCacheStatus { get; init; }

    public static DataStateViewModel<T> NotRequested()
        => new(DataStateDto.NotRequested);

    public static DataStateViewModel<T> Loading()
        => new(DataStateDto.Loading);

    public static DataStateViewModel<T> Loading(string? reason, int? retryAfterSeconds = null, bool showCacheStatus = false)
        => new(DataStateDto.Loading, Reason: reason, RetryAfterSeconds: retryAfterSeconds, ResultStatus: DataStateResultStatus.Loading)
        {
            ShowCacheStatus = showCacheStatus
        };

    public static DataStateViewModel<T> Ready(T data)
        => new(DataStateDto.Available, data);

    public static DataStateViewModel<T> Empty(string? reason = null)
        => new(DataStateDto.Empty, Reason: reason);

    public static DataStateViewModel<T> Failed(string? reason = null)
        => new(DataStateDto.Failed, Reason: reason);

    public static DataStateViewModel<T> Invalid(string? reason = null, T? data = default)
        => new(
            data is null ? DataStateDto.Failed : DataStateDto.Available,
            data,
            reason,
            ResultStatus: DataStateResultStatus.Invalid);

    public static DataStateViewModel<T> Invalid(
        string? reason,
        T? data,
        IReadOnlyList<string>? invalidFields,
        IReadOnlyList<FilterValidationIssueDto>? validationMessages,
        CanonicalFilterMetadata? filterMetadata = null)
        => new(
            data is null ? DataStateDto.Failed : DataStateDto.Available,
            data,
            reason,
            ResultStatus: DataStateResultStatus.Invalid)
        {
            InvalidFields = invalidFields ?? EmptyInvalidFields,
            ValidationMessages = validationMessages ?? EmptyValidationMessages,
            FilterMetadata = filterMetadata
        };

    public static DataStateViewModel<T> FromResponse(DataStateResponseDto<T>? response, string failureReason)
    {
        if (response is null)
        {
            return new DataStateViewModel<T>(DataStateDto.Failed, Reason: failureReason, ResultStatus: DataStateResultStatus.Failed);
        }

        var filterMetadata = ExtractFilterMetadata(response.Data);
        var invalidFields = filterMetadata?.InvalidFields ?? EmptyInvalidFields;
        var validationMessages = filterMetadata?.ValidationMessages ?? EmptyValidationMessages;
        var normalizedState = NormalizeState(response.State);

        return new DataStateViewModel<T>(
            normalizedState,
            response.Data,
            response.Reason,
            response.RetryAfterSeconds,
            DetermineResultStatus(normalizedState, invalidFields))
        {
            InvalidFields = invalidFields,
            ValidationMessages = validationMessages,
            FilterMetadata = filterMetadata,
            ShowCacheStatus = response.State == DataStateDto.NotReady
        };
    }

    public static DataStateViewModel<T> FromResult(DataStateResult<T> result)
        => new(NormalizeState(result.State), result.Data, result.Reason, result.RetryAfterSeconds, NormalizeStatus(result.Status))
        {
            InvalidFields = result.InvalidFields,
            ValidationMessages = result.ValidationMessages,
            FilterMetadata = result.FilterMetadata,
            ShowCacheStatus = result.Status == DataStateResultStatus.NotReady
        };

    private static DataStateResultStatus DetermineResultStatus(DataStateDto state, IReadOnlyList<string> invalidFields)
    {
        if (invalidFields.Count > 0)
        {
            return DataStateResultStatus.Invalid;
        }

        return state switch
        {
            DataStateDto.Available => DataStateResultStatus.Ready,
            DataStateDto.Empty => DataStateResultStatus.Empty,
            DataStateDto.Failed => DataStateResultStatus.Failed,
            DataStateDto.Loading => DataStateResultStatus.Loading,
            _ => DataStateResultStatus.NotRequested
        };
    }

    private static DataStateDto NormalizeState(DataStateDto state)
        => state == DataStateDto.NotReady ? DataStateDto.Loading : state;

    private static DataStateResultStatus NormalizeStatus(DataStateResultStatus status)
        => status == DataStateResultStatus.NotReady ? DataStateResultStatus.Loading : status;

    private static CanonicalFilterMetadata? ExtractFilterMetadata(T? data)
        => ExtractKnownFilterMetadata(data);

    private static CanonicalFilterMetadata? ExtractKnownFilterMetadata(object? data)
    {
        if (data is null)
        {
            return null;
        }

        return data switch
        {
            DeliveryQueryResponseDto<CapacityCalibrationDto> delivery => CanonicalClientResponseFactory.Create(delivery).FilterMetadata,
            DeliveryQueryResponseDto<HomeProductBarMetricsDto> delivery => CanonicalClientResponseFactory.Create(delivery).FilterMetadata,
            DeliveryQueryResponseDto<PortfolioDeliveryDto> delivery => CanonicalClientResponseFactory.Create(delivery).FilterMetadata,
            DeliveryQueryResponseDto<PortfolioProgressTrendDto> delivery => CanonicalClientResponseFactory.Create(delivery).FilterMetadata,
            SprintQueryResponseDto<MultiIterationBacklogHealthDto> sprint => CanonicalClientResponseFactory.Create(sprint).FilterMetadata,
            SprintQueryResponseDto<SprintExecutionDto> sprint => CanonicalClientResponseFactory.Create(sprint).FilterMetadata,
            SprintQueryResponseDto<GetSprintTrendMetricsResponse> sprint => CanonicalClientResponseFactory.Create(sprint).FilterMetadata,
            SprintQueryResponseDto<WorkItemActivityDetailsDto> sprint => CanonicalClientResponseFactory.Create(sprint).FilterMetadata,
            PullRequestQueryResponseDto<GetPrSprintTrendsResponse> pullRequest => CanonicalClientResponseFactory.Create(pullRequest).FilterMetadata,
            PullRequestQueryResponseDto<PullRequestInsightsDto> pullRequest => CanonicalClientResponseFactory.Create(pullRequest).FilterMetadata,
            PullRequestQueryResponseDto<PrDeliveryInsightsDto> pullRequest => CanonicalClientResponseFactory.Create(pullRequest).FilterMetadata,
            PipelineQueryResponseDto<PipelineInsightsDto> pipeline => CanonicalClientResponseFactory.Create(pipeline).FilterMetadata,
            _ => null
        };
    }
}
