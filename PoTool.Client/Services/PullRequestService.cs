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
    public async Task<IEnumerable<PullRequestDto>> GetAllAsync()
    {
        var response = await _pullRequestsClient.GetAllAsync();
        return GeneratedCacheEnvelopeHelper.GetDataOrDefault(
            response,
            static data => data.ToReadOnlyList(),
            Array.Empty<PullRequestDto>());
    }

    /// <summary>
    /// Gets a specific pull request by ID.
    /// </summary>
    public async Task<PullRequestDto?> GetByIdAsync(int id)
    {
        try
        {
            var response = await _pullRequestsClient.GetByIdAsync(id);
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<PullRequestDto>(response);
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
    public async Task<CanonicalClientResponse<IReadOnlyList<SharedPullRequestMetricsDto>>> GetMetricsAsync(string? productIds = null, DateTimeOffset? fromDate = null)
    {
        var response = await _pullRequestsClient.GetMetricsAsync(productIds, fromDate, CancellationToken.None);
        var payload = GeneratedCacheEnvelopeHelper.GetDataOrDefault<object>(response);
        return payload is null
            ? new CanonicalClientResponse<IReadOnlyList<SharedPullRequestMetricsDto>>(Array.Empty<SharedPullRequestMetricsDto>())
            : CanonicalClientResponseFactory.CreateGenerated<IReadOnlyList<SharedPullRequestMetricsDto>>(payload, CanonicalFilterKind.PullRequest);
    }

    /// <summary>
    /// Gets filtered pull requests.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    public async Task<CanonicalClientResponse<IReadOnlyList<SharedPullRequestDto>>> GetFilteredAsync(
        string? productIds = null,
        string? iterationPath = null,
        string? createdBy = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string? status = null)
    {
        var response = await _pullRequestsClient.GetFilteredAsync(
            productIds,
            iterationPath,
            createdBy,
            fromDate,
            toDate,
            status,
            CancellationToken.None);
        var payload = GeneratedCacheEnvelopeHelper.GetDataOrDefault<object>(response);
        return payload is null
            ? new CanonicalClientResponse<IReadOnlyList<SharedPullRequestDto>>(Array.Empty<SharedPullRequestDto>())
            : CanonicalClientResponseFactory.CreateGenerated<IReadOnlyList<SharedPullRequestDto>>(payload, CanonicalFilterKind.PullRequest);
    }

    /// <summary>
    /// Gets comments for a specific pull request.
    /// </summary>
    public async Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId)
    {
        try
        {
            var response = await _pullRequestsClient.GetCommentsAsync(pullRequestId);
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault(
                response,
                static data => data.ToReadOnlyList(),
                Array.Empty<PullRequestCommentDto>());
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
            var response = await _pullRequestsClient.GetIterationsAsync(pullRequestId);
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault(
                response,
                static data => data.ToReadOnlyList(),
                Array.Empty<PullRequestIterationDto>());
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return Array.Empty<PullRequestIterationDto>();
        }
    }
}
