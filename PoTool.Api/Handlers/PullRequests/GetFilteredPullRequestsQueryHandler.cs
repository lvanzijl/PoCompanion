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
    private readonly PullRequestReadProviderFactory _providerFactory;
    private readonly ILogger<GetFilteredPullRequestsQueryHandler> _logger;

    public GetFilteredPullRequestsQueryHandler(
        PullRequestReadProviderFactory providerFactory,
        ILogger<GetFilteredPullRequestsQueryHandler> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestDto>> Handle(
        GetFilteredPullRequestsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetFilteredPullRequestsQuery");

        var provider = _providerFactory.Create();
        var allPrs = await provider.GetByProductIdsAsync(query.ProductIds, cancellationToken);

        // Apply filters
        var filtered = allPrs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.IterationPath))
        {
            filtered = filtered.Where(pr => pr.IterationPath.Contains(query.IterationPath, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.CreatedBy))
        {
            filtered = filtered.Where(pr => pr.CreatedBy.Equals(query.CreatedBy, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FromDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate <= query.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(pr => pr.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }
}
