using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestFileChangesQuery.
/// Uses the cache-backed pull request read provider registered for analytical reads.
/// </summary>
public sealed class GetPullRequestFileChangesQueryHandler : IQueryHandler<GetPullRequestFileChangesQuery, IEnumerable<PullRequestFileChangeDto>>
{
    private readonly IPullRequestReadProvider _pullRequestReadProvider;
    private readonly ILogger<GetPullRequestFileChangesQueryHandler> _logger;

    public GetPullRequestFileChangesQueryHandler(
        IPullRequestReadProvider pullRequestReadProvider,
        ILogger<GetPullRequestFileChangesQueryHandler> logger)
    {
        _pullRequestReadProvider = pullRequestReadProvider;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestFileChangeDto>> Handle(
        GetPullRequestFileChangesQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestFileChangesQuery for PR ID: {PullRequestId}", query.PullRequestId);
        return await _pullRequestReadProvider.GetFileChangesAsync(query.PullRequestId, cancellationToken);
    }
}
