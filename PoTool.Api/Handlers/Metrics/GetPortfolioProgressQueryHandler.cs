using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

public sealed class GetPortfolioProgressQueryHandler
    : IQueryHandler<GetPortfolioProgressQuery, PortfolioProgressDto>
{
    private readonly PortfolioProgressQueryService _queryService;

    public GetPortfolioProgressQueryHandler(PortfolioProgressQueryService queryService)
    {
        _queryService = queryService;
    }

    public ValueTask<PortfolioProgressDto> Handle(
        GetPortfolioProgressQuery query,
        CancellationToken cancellationToken)
        => new(_queryService.GetAsync(query.ProductOwnerId, query.Options, cancellationToken));
}
