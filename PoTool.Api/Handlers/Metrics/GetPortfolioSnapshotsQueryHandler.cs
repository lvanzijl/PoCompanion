using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

public sealed class GetPortfolioSnapshotsQueryHandler
    : IQueryHandler<GetPortfolioSnapshotsQuery, PortfolioSnapshotDto>
{
    private readonly PortfolioSnapshotQueryService _queryService;

    public GetPortfolioSnapshotsQueryHandler(PortfolioSnapshotQueryService queryService)
    {
        _queryService = queryService;
    }

    public ValueTask<PortfolioSnapshotDto> Handle(
        GetPortfolioSnapshotsQuery query,
        CancellationToken cancellationToken)
        => new(_queryService.GetAsync(query.ProductOwnerId, query.Options, cancellationToken));
}
