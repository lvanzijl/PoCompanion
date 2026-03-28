using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetFilteredPullRequestsQuery.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetFilteredPullRequestsQueryHandler : IQueryHandler<GetFilteredPullRequestsQuery, IEnumerable<PullRequestDto>>
{
    private readonly IPullRequestReadProvider _pullRequestReadProvider;
    private readonly ILogger<GetFilteredPullRequestsQueryHandler> _logger;

    public GetFilteredPullRequestsQueryHandler(
        IPullRequestReadProvider pullRequestReadProvider,
        ILogger<GetFilteredPullRequestsQueryHandler> logger)
    {
        _pullRequestReadProvider = pullRequestReadProvider;
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

        var allPrs = await _pullRequestReadProvider.GetByRepositoryNamesAsync(
            query.EffectiveFilter.RepositoryScope,
            query.EffectiveFilter.RangeStartUtc,
            query.EffectiveFilter.RangeEndUtc,
            cancellationToken);

        return PullRequestFiltering.ApplyLocalSelections(allPrs, query.EffectiveFilter);
    }
}
