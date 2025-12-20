using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestFileChangesQuery.
/// </summary>
public sealed class GetPullRequestFileChangesQueryHandler : IQueryHandler<GetPullRequestFileChangesQuery, IEnumerable<PullRequestFileChangeDto>>
{
    private readonly IPullRequestRepository _repository;
    private readonly ILogger<GetPullRequestFileChangesQueryHandler> _logger;

    public GetPullRequestFileChangesQueryHandler(
        IPullRequestRepository repository,
        ILogger<GetPullRequestFileChangesQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestFileChangeDto>> Handle(
        GetPullRequestFileChangesQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestFileChangesQuery for PR ID: {PullRequestId}", query.PullRequestId);
        return await _repository.GetFileChangesAsync(query.PullRequestId, cancellationToken);
    }
}
