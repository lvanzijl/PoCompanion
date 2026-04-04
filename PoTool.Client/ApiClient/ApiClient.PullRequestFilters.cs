using PoTool.Client.Helpers;
using SharedPrDeliveryInsightsDto = PoTool.Shared.PullRequests.PrDeliveryInsightsDto;
using SharedPrInsightsDto = PoTool.Shared.PullRequests.PullRequestInsightsDto;
using SharedPrMetricsDto = PoTool.Shared.PullRequests.PullRequestMetricsDto;
using SharedPrSprintTrendsResponse = PoTool.Shared.PullRequests.GetPrSprintTrendsResponse;
using SharedPullRequestDto = PoTool.Shared.PullRequests.PullRequestDto;

namespace PoTool.Client.ApiClient;

public partial interface IPullRequestsClient
{
    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPrMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPullRequestDto>>> GetFilteredEnvelopeAsync(
        string? productIds,
        string? iterationPath,
        string? createdBy,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? status,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrSprintTrendsResponse>> GetSprintTrendsEnvelopeAsync(
        IEnumerable<int>? sprintIds,
        string? productIds,
        int? teamId,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrInsightsDto>> GetInsightsEnvelopeAsync(
        int? teamId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? repositoryName,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrDeliveryInsightsDto>> GetDeliveryInsightsEnvelopeAsync(
        int? teamId,
        int? sprintId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken);
}

public partial class PullRequestsClient
{
    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPrMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        CancellationToken cancellationToken)
    {
        var response = await GetMetricsAsync(productIds, fromDate, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetMetricsEnvelopeAsync));
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPullRequestDto>>> GetFilteredEnvelopeAsync(
        string? productIds,
        string? iterationPath,
        string? createdBy,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? status,
        CancellationToken cancellationToken)
    {
        var response = await GetFilteredAsync(productIds, iterationPath, createdBy, fromDate, toDate, status, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetFilteredEnvelopeAsync));
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrSprintTrendsResponse>> GetSprintTrendsEnvelopeAsync(
        IEnumerable<int>? sprintIds,
        string? productIds,
        int? teamId,
        CancellationToken cancellationToken)
    {
        var response = await GetSprintTrendsAsync(sprintIds, productIds, teamId, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetSprintTrendsEnvelopeAsync));
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrInsightsDto>> GetInsightsEnvelopeAsync(
        int? teamId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? repositoryName,
        CancellationToken cancellationToken)
    {
        var response = await GetInsightsAsync(teamId, fromDate, toDate, repositoryName, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetInsightsEnvelopeAsync));
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrDeliveryInsightsDto>> GetDeliveryInsightsEnvelopeAsync(
        int? teamId,
        int? sprintId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        var response = await GetDeliveryInsightsAsync(teamId, sprintId, fromDate, toDate, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetDeliveryInsightsEnvelopeAsync));
    }
}
