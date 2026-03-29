using Mediator;
using PoTool.Api.Services;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetFilteredPullRequestsQuery.
/// Uses the analytical pull request query store for repository-scoped cached reads.
/// </summary>
public sealed class GetFilteredPullRequestsQueryHandler : IQueryHandler<GetFilteredPullRequestsQuery, IEnumerable<PullRequestDto>>
{
    private readonly IPullRequestQueryStore _queryStore;
    private readonly ILogger<GetFilteredPullRequestsQueryHandler> _logger;

    public GetFilteredPullRequestsQueryHandler(
        IPullRequestQueryStore queryStore,
        ILogger<GetFilteredPullRequestsQueryHandler> logger)
    {
        _queryStore = queryStore;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestDto>> Handle(
        GetFilteredPullRequestsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetFilteredPullRequestsQuery with repository scope {RepositoryCount}, range [{RangeStartUtc}, {RangeEndUtc}]",
            query.EffectiveFilter.RepositoryScope.Count,
            query.EffectiveFilter.RangeStartUtc,
            query.EffectiveFilter.RangeEndUtc);

        var allPrs = await _queryStore.GetScopedPullRequestsAsync(
            query.EffectiveFilter,
            cancellationToken);

        return PullRequestFiltering.ApplyLocalSelections(allPrs, query.EffectiveFilter);
    }
}
