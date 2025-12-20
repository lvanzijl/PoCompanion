using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestCommentsQuery.
/// </summary>
public sealed class GetPullRequestCommentsQueryHandler : IQueryHandler<GetPullRequestCommentsQuery, IEnumerable<PullRequestCommentDto>>
{
    private readonly IPullRequestRepository _repository;
    private readonly ILogger<GetPullRequestCommentsQueryHandler> _logger;

    public GetPullRequestCommentsQueryHandler(
        IPullRequestRepository repository,
        ILogger<GetPullRequestCommentsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestCommentDto>> Handle(
        GetPullRequestCommentsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestCommentsQuery for PR ID: {PullRequestId}", query.PullRequestId);
        return await _repository.GetCommentsAsync(query.PullRequestId, cancellationToken);
    }
}
