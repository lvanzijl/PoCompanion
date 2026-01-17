using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestFileChangesQuery.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetPullRequestFileChangesQueryHandler : IQueryHandler<GetPullRequestFileChangesQuery, IEnumerable<PullRequestFileChangeDto>>
{
    private readonly PullRequestReadProviderFactory _providerFactory;
    private readonly ILogger<GetPullRequestFileChangesQueryHandler> _logger;

    public GetPullRequestFileChangesQueryHandler(
        PullRequestReadProviderFactory providerFactory,
        ILogger<GetPullRequestFileChangesQueryHandler> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestFileChangeDto>> Handle(
        GetPullRequestFileChangesQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetPullRequestFileChangesQuery for PR ID: {PullRequestId}", query.PullRequestId);
        var provider = _providerFactory.Create();
        return await provider.GetFileChangesAsync(query.PullRequestId, cancellationToken);
    }
}
