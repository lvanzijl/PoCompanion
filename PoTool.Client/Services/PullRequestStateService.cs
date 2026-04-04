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
        => GetInsightsStateCoreAsync(teamId, repositoryName, cancellationToken);

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<PrDeliveryInsightsDto>>?> GetDeliveryInsightsStateAsync(
        int productOwnerId,
        int? sprintId,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetDeliveryInsightsStateCoreAsync(teamId, sprintId, cancellationToken);

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<GetPrSprintTrendsResponse>>?> GetSprintTrendsStateAsync(
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetSprintTrendsStateCoreAsync(sprintIds, productIds, teamId, cancellationToken);

    private async Task<DataStateResponseDto<PullRequestQueryResponseDto<PullRequestInsightsDto>>?> GetInsightsStateCoreAsync(
        int? teamId,
        string? repositoryName,
        CancellationToken cancellationToken)
        => (await _pullRequestsClient.GetInsightsAsync(
                teamId,
                fromDate: null,
                toDate: null,
                repositoryName,
                cancellationToken))
            .ToDataStateResponse();

    private async Task<DataStateResponseDto<PullRequestQueryResponseDto<PrDeliveryInsightsDto>>?> GetDeliveryInsightsStateCoreAsync(
        int? teamId,
        int? sprintId,
        CancellationToken cancellationToken)
        => (await _pullRequestsClient.GetDeliveryInsightsAsync(
                teamId,
                sprintId,
                fromDate: null,
                toDate: null,
                cancellationToken))
            .ToDataStateResponse();

    private async Task<DataStateResponseDto<PullRequestQueryResponseDto<GetPrSprintTrendsResponse>>?> GetSprintTrendsStateCoreAsync(
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        int? teamId,
        CancellationToken cancellationToken)
        => (await _pullRequestsClient.GetSprintTrendsAsync(
                sprintIds,
                productIds is null ? null : string.Join(",", productIds),
                teamId,
                cancellationToken))
            .ToDataStateResponse();

    private static async Task<DataStateResponseDto<TData>?> GetAsync<TEnvelope, TData>(Task<TEnvelope> requestTask)
        where TEnvelope : class, IGeneratedDataStateEnvelope<TData>
    {
        return (await requestTask.ConfigureAwait(false)).ToDataStateResponse();
    }

}
