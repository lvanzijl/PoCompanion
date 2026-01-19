using PoTool.Client.ApiClient;

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
    public async Task<IEnumerable<PullRequestMetricsDto>> GetMetricsAsync(string? productIds = null)
    {
        return await _pullRequestsClient.GetMetricsAsync(productIds) ?? Array.Empty<PullRequestMetricsDto>();
    }

    /// <summary>
    /// Gets filtered pull requests.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    public async Task<IEnumerable<PullRequestDto>> GetFilteredAsync(
        string? productIds = null,
        string? iterationPath = null,
        string? createdBy = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string? status = null)
    {
        return await _pullRequestsClient.GetFilteredAsync(productIds, iterationPath, createdBy, fromDate, toDate, status)
            ?? Array.Empty<PullRequestDto>();
    }

    /// <summary>
    /// Synchronizes pull requests from TFS.
    /// NOTE: Sync endpoint removed from API - this method is obsolete.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    [Obsolete("Sync endpoint no longer exists in API")]
    public async Task<int> SyncAsync(string? productIds = null)
    {
        // TODO: Restore sync functionality or remove this method
        await Task.CompletedTask;
        return 0;
    }
}
