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
    private readonly PullRequestReadProviderFactory _providerFactory;
    private readonly ILogger<GetPullRequestCommentsQueryHandler> _logger;

    public GetPullRequestCommentsQueryHandler(
        PullRequestReadProviderFactory providerFactory,
        ILogger<GetPullRequestCommentsQueryHandler> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestCommentDto>> Handle(
        GetPullRequestCommentsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestCommentsQuery for PR ID: {PullRequestId}", query.PullRequestId);
        var provider = _providerFactory.Create();
        return await provider.GetCommentsAsync(query.PullRequestId, cancellationToken);
    }
}
