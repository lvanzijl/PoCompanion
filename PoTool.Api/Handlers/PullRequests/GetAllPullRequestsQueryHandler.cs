using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetAllPullRequestsQuery.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllPullRequestsQueryHandler : IQueryHandler<GetAllPullRequestsQuery, IEnumerable<PullRequestDto>>
{
    private readonly IPullRequestReadProvider _pullRequestReadProvider;
    private readonly ILogger<GetAllPullRequestsQueryHandler> _logger;

    public GetAllPullRequestsQueryHandler(
        IPullRequestReadProvider pullRequestReadProvider,
        ILogger<GetAllPullRequestsQueryHandler> logger)
    {
        _pullRequestReadProvider = pullRequestReadProvider;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestDto>> Handle(
        GetAllPullRequestsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllPullRequestsQuery");
        // Live-only mode: use injected provider directly
        return await _pullRequestReadProvider.GetAllAsync(cancellationToken);
    }
}
