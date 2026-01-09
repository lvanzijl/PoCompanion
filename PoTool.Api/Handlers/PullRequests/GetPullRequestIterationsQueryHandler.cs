using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestIterationsQuery.
/// </summary>
public sealed class GetPullRequestIterationsQueryHandler : IQueryHandler<GetPullRequestIterationsQuery, IEnumerable<PullRequestIterationDto>>
{
    private readonly IPullRequestRepository _repository;
    private readonly ILogger<GetPullRequestIterationsQueryHandler> _logger;

    public GetPullRequestIterationsQueryHandler(
        IPullRequestRepository repository,
        ILogger<GetPullRequestIterationsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestIterationDto>> Handle(
        GetPullRequestIterationsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestIterationsQuery for PR ID: {PullRequestId}", query.PullRequestId);
        return await _repository.GetIterationsAsync(query.PullRequestId, cancellationToken);
    }
}
