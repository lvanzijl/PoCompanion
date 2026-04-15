using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using SharedPullRequestMetricsDto = PoTool.Shared.PullRequests.PullRequestMetricsDto;
using SharedPullRequestDto = PoTool.Shared.PullRequests.PullRequestDto;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with the Pull Requests API.
/// </summary>
public class PullRequestService
{
    private readonly IPullRequestsClient _pullRequestsClient;

    public PullRequestService(IPullRequestsClient pullRequestsClient)
    {
        _pullRequestsClient = pullRequestsClient;
    }

    /// <summary>
    /// Gets all cached pull requests.
    /// </summary>
    public async Task<DataStateResult<IReadOnlyList<PullRequestDto>>> GetAllAsync()
        => GeneratedCacheEnvelopeHelper.ToReadOnlyListDataStateResult(
            await _pullRequestsClient.GetAllAsync());

    /// <summary>
    /// Gets a specific pull request by ID.
    /// </summary>
    public async Task<DataStateResult<PullRequestDto>> GetByIdAsync(int id)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.ToDataStateResult(
                await _pullRequestsClient.GetByIdAsync(id));
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return DataStateResult<PullRequestDto>.Empty("The requested pull request was not found.");
        }
    }

    /// <summary>
    /// Gets aggregated metrics for pull requests.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    /// <param name="fromDate">Optional start date filter</param>
    public async Task<DataStateResult<IReadOnlyList<SharedPullRequestMetricsDto>>> GetMetricsAsync(string? productIds = null, DateTimeOffset? fromDate = null)
    {
        return (await _pullRequestsClient.GetMetricsAsync(productIds, fromDate, CancellationToken.None))
            .ToDataStateResponse()
            .ToDataStateResult();
    }

    /// <summary>
    /// Gets filtered pull requests.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    public async Task<DataStateResult<IReadOnlyList<SharedPullRequestDto>>> GetFilteredAsync(
        string? productIds = null,
        string? iterationPath = null,
        string? createdBy = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string? status = null)
    {
        return (await _pullRequestsClient.GetFilteredAsync(
            productIds,
            iterationPath,
            createdBy,
            fromDate,
            toDate,
            status,
            CancellationToken.None))
            .ToDataStateResponse()
            .ToDataStateResult();
    }

    /// <summary>
    /// Gets comments for a specific pull request.
    /// </summary>
    public async Task<DataStateResult<IReadOnlyList<PullRequestCommentDto>>> GetCommentsAsync(int pullRequestId)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.ToReadOnlyListDataStateResult(
                await _pullRequestsClient.GetCommentsAsync(pullRequestId));
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return DataStateResult<IReadOnlyList<PullRequestCommentDto>>.Empty("No comments were found for the requested pull request.");
        }
    }

    /// <summary>
    /// Gets iterations for a specific pull request.
    /// </summary>
    public async Task<DataStateResult<IReadOnlyList<PullRequestIterationDto>>> GetIterationsAsync(int pullRequestId)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.ToReadOnlyListDataStateResult(
                await _pullRequestsClient.GetIterationsAsync(pullRequestId));
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return DataStateResult<IReadOnlyList<PullRequestIterationDto>>.Empty("No iterations were found for the requested pull request.");
        }
    }
}
