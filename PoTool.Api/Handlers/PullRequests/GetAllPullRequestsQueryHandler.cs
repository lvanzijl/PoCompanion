using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetAllPullRequestsQuery.
/// </summary>
public sealed class GetAllPullRequestsQueryHandler : IQueryHandler<GetAllPullRequestsQuery, IEnumerable<PullRequestDto>>
{
    private readonly IPullRequestRepository _repository;
    private readonly ILogger<GetAllPullRequestsQueryHandler> _logger;

    public GetAllPullRequestsQueryHandler(
        IPullRequestRepository repository,
        ILogger<GetAllPullRequestsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestDto>> Handle(
        GetAllPullRequestsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllPullRequestsQuery");
        return await _repository.GetAllAsync(cancellationToken);
    }
}
