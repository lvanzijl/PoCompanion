using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

public sealed class GetPortfolioSignalsQueryHandler
    : IQueryHandler<GetPortfolioSignalsQuery, PortfolioSignalsDto>
{
    private readonly PortfolioDecisionSignalQueryService _queryService;

    public GetPortfolioSignalsQueryHandler(PortfolioDecisionSignalQueryService queryService)
    {
        _queryService = queryService;
    }

    public ValueTask<PortfolioSignalsDto> Handle(
        GetPortfolioSignalsQuery query,
        CancellationToken cancellationToken)
        => new(_queryService.GetAsync(query.ProductOwnerId, query.Options, cancellationToken));
}
