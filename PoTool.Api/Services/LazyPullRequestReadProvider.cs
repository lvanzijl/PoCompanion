using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

/// <summary>
/// Lazy wrapper for IPullRequestReadProvider that delays provider resolution until method calls.
/// This ensures the DataSourceModeMiddleware has set the correct mode before resolving the provider.
/// </summary>
public sealed class LazyPullRequestReadProvider : IPullRequestReadProvider
{
    private readonly DataSourceAwareReadProviderFactory _factory;

    public LazyPullRequestReadProvider(DataSourceAwareReadProviderFactory factory)
    {
        _factory = factory;
    }

    public Task<IEnumerable<PullRequestDto>> GetAllAsync(DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetAllAsync(fromDate, cancellationToken);
    }

    public Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetByProductIdsAsync(productIds, fromDate, cancellationToken);
    }

    public Task<IEnumerable<PullRequestDto>> GetByRepositoryNamesAsync(
        IReadOnlyList<string> repositoryNames,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetByRepositoryNamesAsync(repositoryNames, fromDate, toDate, cancellationToken);
    }

    public Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetByIdAsync(pullRequestId, cancellationToken);
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetIterationsAsync(pullRequestId, cancellationToken);
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetIterationsAsync(pullRequestId, repositoryName, cancellationToken);
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetCommentsAsync(pullRequestId, cancellationToken);
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetCommentsAsync(pullRequestId, repositoryName, cancellationToken);
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetFileChangesAsync(pullRequestId, cancellationToken);
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        return _factory.GetPullRequestReadProvider().GetFileChangesAsync(pullRequestId, repositoryName, cancellationToken);
    }
}
