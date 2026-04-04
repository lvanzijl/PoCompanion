using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Shared.DataState;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Services;

public sealed class PullRequestStateService
{
    private readonly IPullRequestsClient _pullRequestsClient;

    public PullRequestStateService(IPullRequestsClient pullRequestsClient)
    {
        _pullRequestsClient = pullRequestsClient;
    }

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<PullRequestInsightsDto>>?> GetInsightsStateAsync(
        int productOwnerId,
        int? sprintId,
        int? teamId,
        string? repositoryName,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfPullRequestQueryResponseDtoOfPullRequestInsightsDto, PullRequestQueryResponseDtoOfPullRequestInsightsDto, PullRequestQueryResponseDto<PullRequestInsightsDto>>(
            _pullRequestsClient.GetInsightsAsync(
                teamId,
                fromDate: null,
                toDate: null,
                repositoryName,
                cancellationToken),
            static data => data.ToShared());

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<PrDeliveryInsightsDto>>?> GetDeliveryInsightsStateAsync(
        int productOwnerId,
        int? sprintId,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfPullRequestQueryResponseDtoOfPrDeliveryInsightsDto, PullRequestQueryResponseDtoOfPrDeliveryInsightsDto, PullRequestQueryResponseDto<PrDeliveryInsightsDto>>(
            _pullRequestsClient.GetDeliveryInsightsAsync(
                teamId,
                sprintId,
                fromDate: null,
                toDate: null,
                cancellationToken),
            static data => data.ToShared());

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<GetPrSprintTrendsResponse>>?> GetSprintTrendsStateAsync(
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfPullRequestQueryResponseDtoOfGetPrSprintTrendsResponse, PullRequestQueryResponseDtoOfGetPrSprintTrendsResponse, PullRequestQueryResponseDto<GetPrSprintTrendsResponse>>(
            _pullRequestsClient.GetSprintTrendsAsync(sprintIds, productIds is null ? null : string.Join(",", productIds), teamId, cancellationToken),
            static data => data.ToShared());

    private static async Task<DataStateResponseDto<TData>?> GetAsync<TEnvelope, TData>(Task<TEnvelope> requestTask)
        where TEnvelope : class, IGeneratedDataStateEnvelope<TData>
    {
        return GeneratedCacheEnvelopeHelper.ToDataStateResponse(await requestTask.ConfigureAwait(false));
    }

    private static async Task<DataStateResponseDto<TMapped>?> GetAsync<TEnvelope, TSource, TMapped>(
        Task<TEnvelope> requestTask,
        Func<TSource, TMapped?> mapper)
        where TEnvelope : class, IGeneratedDataStateEnvelope<TSource>
    {
        return GeneratedCacheEnvelopeHelper.ToDataStateResponse(
            await requestTask.ConfigureAwait(false),
            mapper);
    }
}
