using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetFilteredPullRequestsQuery.
/// </summary>
public sealed class GetFilteredPullRequestsQueryHandler : IQueryHandler<GetFilteredPullRequestsQuery, IEnumerable<PullRequestDto>>
{
    private readonly IPullRequestRepository _repository;
    private readonly ILogger<GetFilteredPullRequestsQueryHandler> _logger;

    public GetFilteredPullRequestsQueryHandler(
        IPullRequestRepository repository,
        ILogger<GetFilteredPullRequestsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestDto>> Handle(
        GetFilteredPullRequestsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetFilteredPullRequestsQuery");

        var allPrs = await _repository.GetByProductIdsAsync(query.ProductIds, cancellationToken);

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

        // Timeframe filtering by weeks/months (converted to date range)
        if (query.LastNWeeks.HasValue && query.LastNWeeks.Value > 0)
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7 * query.LastNWeeks.Value);
            filtered = filtered.Where(pr => pr.CreatedDate >= cutoffDate);
        }
        else if (query.LastNMonths.HasValue && query.LastNMonths.Value > 0)
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddMonths(-query.LastNMonths.Value);
            filtered = filtered.Where(pr => pr.CreatedDate >= cutoffDate);
        }
        // Specific iteration key filtering (note: IterationPath contains TFS iteration, not timeframe iteration)
        // TimeframeIterationKey would require a join, so it's omitted for now in favor of date filtering
        // Explicit date range filtering
        else if (query.FromDate.HasValue || query.ToDate.HasValue)
        {
            if (query.FromDate.HasValue)
            {
                filtered = filtered.Where(pr => pr.CreatedDate >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                filtered = filtered.Where(pr => pr.CreatedDate <= query.ToDate.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(pr => pr.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }
}
