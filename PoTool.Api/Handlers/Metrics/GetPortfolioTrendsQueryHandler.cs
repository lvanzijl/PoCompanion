using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

public sealed class GetPortfolioTrendsQueryHandler
    : IQueryHandler<GetPortfolioTrendsQuery, PortfolioTrendDto>
{
    private readonly PortfolioTrendQueryService _queryService;

    public GetPortfolioTrendsQueryHandler(PortfolioTrendQueryService queryService)
    {
        _queryService = queryService;
    }

    public ValueTask<PortfolioTrendDto> Handle(
        GetPortfolioTrendsQuery query,
        CancellationToken cancellationToken)
        => new(_queryService.GetAsync(query.ProductOwnerId, query.Options, cancellationToken));
}
