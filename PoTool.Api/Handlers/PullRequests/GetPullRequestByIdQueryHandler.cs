using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestByIdQuery.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetPullRequestByIdQueryHandler : IQueryHandler<GetPullRequestByIdQuery, PullRequestDto?>
{
    private readonly IPullRequestReadProvider _pullRequestReadProvider;
    private readonly ILogger<GetPullRequestByIdQueryHandler> _logger;

    public GetPullRequestByIdQueryHandler(
        IPullRequestReadProvider pullRequestReadProvider,
        ILogger<GetPullRequestByIdQueryHandler> logger)
    {
        _pullRequestReadProvider = pullRequestReadProvider;
        _logger = logger;
    }

    public async ValueTask<PullRequestDto?> Handle(
        GetPullRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestByIdQuery for PR ID: {PullRequestId}", query.PullRequestId);
        // Live-only mode: use injected provider directly
        return await _pullRequestReadProvider.GetByIdAsync(query.PullRequestId, cancellationToken);
    }
}
