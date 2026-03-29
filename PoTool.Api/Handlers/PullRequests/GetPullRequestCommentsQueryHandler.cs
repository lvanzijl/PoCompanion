using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestCommentsQuery.
/// Uses the cache-backed pull request read provider registered for analytical reads.
/// </summary>
public sealed class GetPullRequestCommentsQueryHandler : IQueryHandler<GetPullRequestCommentsQuery, IEnumerable<PullRequestCommentDto>>
{
    private readonly IPullRequestReadProvider _pullRequestReadProvider;
    private readonly ILogger<GetPullRequestCommentsQueryHandler> _logger;

    public GetPullRequestCommentsQueryHandler(
        IPullRequestReadProvider pullRequestReadProvider,
        ILogger<GetPullRequestCommentsQueryHandler> logger)
    {
        _pullRequestReadProvider = pullRequestReadProvider;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestCommentDto>> Handle(
        GetPullRequestCommentsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestCommentsQuery for PR ID: {PullRequestId}", query.PullRequestId);
        return await _pullRequestReadProvider.GetCommentsAsync(query.PullRequestId, cancellationToken);
    }
}
