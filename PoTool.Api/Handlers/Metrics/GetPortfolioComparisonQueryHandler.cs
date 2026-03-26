using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

public sealed class GetPortfolioComparisonQueryHandler
    : IQueryHandler<GetPortfolioComparisonQuery, PortfolioComparisonDto>
{
    private readonly PortfolioComparisonQueryService _queryService;

    public GetPortfolioComparisonQueryHandler(PortfolioComparisonQueryService queryService)
    {
        _queryService = queryService;
    }

    public ValueTask<PortfolioComparisonDto> Handle(
        GetPortfolioComparisonQuery query,
        CancellationToken cancellationToken)
        => new(_queryService.GetAsync(query.ProductOwnerId, query.Options, cancellationToken));
}
