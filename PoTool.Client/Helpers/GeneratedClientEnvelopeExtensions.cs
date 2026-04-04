using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.BuildQuality;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Helpers;

public static class GeneratedClientEnvelopeExtensions
{
    public static CacheBackedClientResult<TData> ToCacheBackedResult<TData>(this IGeneratedDataStateEnvelope<TData> envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope);

    public static DataStateResponseDto<TData> ToDataStateResponse<TData>(this IGeneratedDataStateEnvelope<TData> envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope);

    public static TData GetDataOrDefault<TData>(this IGeneratedDataStateEnvelope<TData> envelope, TData defaultValue)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, defaultValue);

    public static TData? GetDataOrDefault<TData>(this IGeneratedDataStateEnvelope<TData> envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope);

    public static IReadOnlyList<TItem> GetReadOnlyListOrDefault<TItem>(
        this IGeneratedDataStateEnvelope<ICollection<TItem>> envelope,
        IReadOnlyCollection<TItem>? defaultValue = null)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(
            envelope,
            static data => data.ToReadOnlyList(),
            defaultValue is null ? Array.Empty<TItem>() : defaultValue.ToList());

    public static DataStateResponseDto<IReadOnlyList<TItem>> ToReadOnlyListDataStateResponse<TItem>(
        this IGeneratedDataStateEnvelope<ICollection<TItem>> envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(
            envelope,
            static data => data.ToReadOnlyList());

    public static CacheBackedClientResult<DeliveryQueryResponseDto<BuildQualityPageDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfBuildQualityPageDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<DeliveryQueryResponseDto<DeliveryBuildQualityDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfDeliveryBuildQualityDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<DeliveryQueryResponseDto<PortfolioProgressTrendDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioProgressTrendDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<DeliveryQueryResponseDto<PortfolioDeliveryDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioDeliveryDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<DeliveryQueryResponseDto<CapacityCalibrationDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfCapacityCalibrationDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<DeliveryQueryResponseDto<HomeProductBarMetricsDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfHomeProductBarMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<SprintQueryResponseDto<SprintMetricsDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfSprintMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<SprintQueryResponseDto<BacklogHealthDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfBacklogHealthDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<SprintQueryResponseDto<MultiIterationBacklogHealthDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfMultiIterationBacklogHealthDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<SprintQueryResponseDto<SprintCapacityPlanDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfSprintCapacityPlanDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<SprintQueryResponseDto<GetSprintTrendMetricsResponse>> ToCacheBackedResult(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfGetSprintTrendMetricsResponse envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<SprintQueryResponseDto<SprintExecutionDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfSprintExecutionDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<SprintQueryResponseDto<WorkItemActivityDetailsDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfWorkItemActivityDetailsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>> ToCacheBackedResult(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>> ToCacheBackedResult(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineRunDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PipelineQueryResponseDto<PipelineInsightsDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfPipelineInsightsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PullRequestQueryResponseDto<IReadOnlyList<PullRequestMetricsDto>>> ToCacheBackedResult(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PullRequestQueryResponseDto<IReadOnlyList<PullRequestDto>>> ToCacheBackedResult(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PullRequestQueryResponseDto<GetPrSprintTrendsResponse>> ToCacheBackedResult(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfGetPrSprintTrendsResponse envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PullRequestQueryResponseDto<PullRequestInsightsDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfPullRequestInsightsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static CacheBackedClientResult<PullRequestQueryResponseDto<PrDeliveryInsightsDto>> ToCacheBackedResult(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfPrDeliveryInsightsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToCacheBackedResult(envelope, static data => data.ToShared());

    public static DataStateResponseDto<SprintQueryResponseDto<MultiIterationBacklogHealthDto>> ToDataStateResponse(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfMultiIterationBacklogHealthDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<DeliveryQueryResponseDto<CapacityCalibrationDto>> ToDataStateResponse(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfCapacityCalibrationDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<DeliveryQueryResponseDto<PortfolioDeliveryDto>> ToDataStateResponse(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioDeliveryDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<SprintQueryResponseDto<GetSprintTrendMetricsResponse>> ToDataStateResponse(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfGetSprintTrendMetricsResponse envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<SprintQueryResponseDto<SprintExecutionDto>> ToDataStateResponse(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfSprintExecutionDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<SprintQueryResponseDto<WorkItemActivityDetailsDto>> ToDataStateResponse(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfWorkItemActivityDetailsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<DeliveryQueryResponseDto<PortfolioProgressTrendDto>> ToDataStateResponse(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioProgressTrendDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<PipelineQueryResponseDto<PipelineInsightsDto>> ToDataStateResponse(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfPipelineInsightsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<PullRequestQueryResponseDto<PullRequestInsightsDto>> ToDataStateResponse(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfPullRequestInsightsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<PullRequestQueryResponseDto<PrDeliveryInsightsDto>> ToDataStateResponse(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfPrDeliveryInsightsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<PullRequestQueryResponseDto<GetPrSprintTrendsResponse>> ToDataStateResponse(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfGetPrSprintTrendsResponse envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DeliveryQueryResponseDto<PortfolioProgressTrendDto>? GetDataOrDefault(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioProgressTrendDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static DeliveryQueryResponseDto<PortfolioDeliveryDto>? GetDataOrDefault(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioDeliveryDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static DeliveryQueryResponseDto<HomeProductBarMetricsDto>? GetDataOrDefault(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfHomeProductBarMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static SprintQueryResponseDto<GetSprintTrendMetricsResponse>? GetDataOrDefault(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfGetSprintTrendMetricsResponse envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static SprintQueryResponseDto<BacklogHealthDto>? GetDataOrDefault(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfBacklogHealthDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static SprintQueryResponseDto<SprintExecutionDto>? GetDataOrDefault(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfSprintExecutionDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static PullRequestQueryResponseDto<GetPrSprintTrendsResponse>? GetDataOrDefault(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfGetPrSprintTrendsResponse envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static DeliveryQueryResponseDto<CapacityCalibrationDto>? GetDataOrDefault(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfCapacityCalibrationDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static PullRequestQueryResponseDto<IReadOnlyList<PullRequestMetricsDto>>? GetDataOrDefault(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static PullRequestQueryResponseDto<IReadOnlyList<PullRequestDto>>? GetDataOrDefault(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>? GetDataOrDefault(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());

    public static PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>? GetDataOrDefault(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineRunDto envelope)
        => GeneratedCacheEnvelopeHelper.GetDataOrDefault(envelope, static data => data.ToShared());
}
