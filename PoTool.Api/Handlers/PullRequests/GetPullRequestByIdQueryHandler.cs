using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestByIdQuery.
/// </summary>
public sealed class GetPullRequestByIdQueryHandler : IQueryHandler<GetPullRequestByIdQuery, PullRequestDto?>
{
    private readonly IPullRequestRepository _repository;
    private readonly ILogger<GetPullRequestByIdQueryHandler> _logger;

    public GetPullRequestByIdQueryHandler(
        IPullRequestRepository repository,
        ILogger<GetPullRequestByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<PullRequestDto?> Handle(
        GetPullRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestByIdQuery for PR ID: {PullRequestId}", query.PullRequestId);
        return await _repository.GetByIdAsync(query.PullRequestId, cancellationToken);
    }
}
