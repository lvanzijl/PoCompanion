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

    public static DataStateResult<TData> ToDataStateResult<TData>(this IGeneratedDataStateEnvelope<TData> envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResult(envelope);

    public static DataStateResponseDto<IReadOnlyList<TItem>> ToReadOnlyListDataStateResponse<TItem>(
        this IGeneratedDataStateEnvelope<ICollection<TItem>> envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(
            envelope,
            static data => data.ToReadOnlyList());

    public static DataStateResult<IReadOnlyList<TItem>> ToReadOnlyListDataStateResult<TItem>(
        this IGeneratedDataStateEnvelope<ICollection<TItem>> envelope)
        => GeneratedCacheEnvelopeHelper.ToReadOnlyListDataStateResult(envelope);

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

    public static DataStateResponseDto<SprintQueryResponseDto<BacklogHealthDto>> ToDataStateResponse(
        this DataStateResponseDtoOfSprintQueryResponseDtoOfBacklogHealthDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<DeliveryQueryResponseDto<CapacityCalibrationDto>> ToDataStateResponse(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfCapacityCalibrationDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<DeliveryQueryResponseDto<PortfolioDeliveryDto>> ToDataStateResponse(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioDeliveryDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<DeliveryQueryResponseDto<HomeProductBarMetricsDto>> ToDataStateResponse(
        this DataStateResponseDtoOfDeliveryQueryResponseDtoOfHomeProductBarMetricsDto envelope)
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

    public static DataStateResponseDto<PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>> ToDataStateResponse(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>> ToDataStateResponse(
        this DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineRunDto envelope)
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

    public static DataStateResponseDto<PullRequestQueryResponseDto<IReadOnlyList<PullRequestMetricsDto>>> ToDataStateResponse(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestMetricsDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

    public static DataStateResponseDto<PullRequestQueryResponseDto<IReadOnlyList<PullRequestDto>>> ToDataStateResponse(
        this DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestDto envelope)
        => GeneratedCacheEnvelopeHelper.ToDataStateResponse(envelope, static data => data.ToShared());

}
