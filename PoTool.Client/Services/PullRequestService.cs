using PoTool.Client.ApiClient;
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
    public async Task<IEnumerable<PullRequestDto>> GetAllAsync()
    {
        return await _pullRequestsClient.GetAllAsync() ?? Array.Empty<PullRequestDto>();
    }

    /// <summary>
    /// Gets a specific pull request by ID.
    /// </summary>
    public async Task<PullRequestDto?> GetByIdAsync(int id)
    {
        try
        {
            return await _pullRequestsClient.GetByIdAsync(id);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets aggregated metrics for pull requests.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    /// <param name="fromDate">Optional start date filter</param>
    public async Task<IEnumerable<SharedPullRequestMetricsDto>> GetMetricsAsync(string? productIds = null, DateTimeOffset? fromDate = null)
    {
        var response = await _pullRequestsClient.GetMetricsEnvelopeAsync(productIds, fromDate, CancellationToken.None);
        return response.Data;
    }

    /// <summary>
    /// Gets filtered pull requests.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    public async Task<IEnumerable<SharedPullRequestDto>> GetFilteredAsync(
        string? productIds = null,
        string? iterationPath = null,
        string? createdBy = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string? status = null)
    {
        var response = await _pullRequestsClient.GetFilteredEnvelopeAsync(
            productIds,
            iterationPath,
            createdBy,
            fromDate,
            toDate,
            status,
            CancellationToken.None);
        return response.Data;
    }

    /// <summary>
    /// Gets comments for a specific pull request.
    /// </summary>
    public async Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId)
    {
        try
        {
            return await _pullRequestsClient.GetCommentsAsync(pullRequestId) ?? Array.Empty<PullRequestCommentDto>();
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return Array.Empty<PullRequestCommentDto>();
        }
    }

    /// <summary>
    /// Gets iterations for a specific pull request.
    /// </summary>
    public async Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId)
    {
        try
        {
            return await _pullRequestsClient.GetIterationsAsync(pullRequestId) ?? Array.Empty<PullRequestIterationDto>();
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return Array.Empty<PullRequestIterationDto>();
        }
    }
}
