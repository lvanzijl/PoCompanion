using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

/// <summary>
/// Cached pull request read provider that delegates to the existing repository.
/// Used when DataSourceMode is Cached.
/// </summary>
public sealed class CachedPullRequestReadProvider : IPullRequestReadProvider
{
    private readonly IPullRequestRepository _repository;

    public CachedPullRequestReadProvider(IPullRequestRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<PullRequestDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, CancellationToken cancellationToken = default)
    {
        return _repository.GetByProductIdsAsync(productIds, cancellationToken);
    }

    public Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(pullRequestId, cancellationToken);
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _repository.GetIterationsAsync(pullRequestId, cancellationToken);
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _repository.GetCommentsAsync(pullRequestId, cancellationToken);
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        return _repository.GetFileChangesAsync(pullRequestId, cancellationToken);
    }
}
