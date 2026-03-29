using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestIterationsQuery.
/// Uses the cache-backed pull request read provider registered for analytical reads.
/// </summary>
public sealed class GetPullRequestIterationsQueryHandler : IQueryHandler<GetPullRequestIterationsQuery, IEnumerable<PullRequestIterationDto>>
{
    private readonly IPullRequestReadProvider _pullRequestReadProvider;
    private readonly ILogger<GetPullRequestIterationsQueryHandler> _logger;

    public GetPullRequestIterationsQueryHandler(
        IPullRequestReadProvider pullRequestReadProvider,
        ILogger<GetPullRequestIterationsQueryHandler> logger)
    {
        _pullRequestReadProvider = pullRequestReadProvider;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestIterationDto>> Handle(
        GetPullRequestIterationsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestIterationsQuery for PR ID: {PullRequestId}", query.PullRequestId);
        return await _pullRequestReadProvider.GetIterationsAsync(query.PullRequestId, cancellationToken);
    }
}
