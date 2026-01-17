using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestCommentsQuery.
/// Uses read provider to support both Live and Cached modes.
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
        // Live-only mode: use injected provider directly
        return await _pullRequestReadProvider.GetCommentsAsync(query.PullRequestId, cancellationToken);
    }
}
