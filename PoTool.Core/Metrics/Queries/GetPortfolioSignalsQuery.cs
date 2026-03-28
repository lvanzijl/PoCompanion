using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to read deterministic decision-support signals derived from persisted portfolio history.
/// </summary>
public sealed record GetPortfolioSignalsQuery(
    int ProductOwnerId,
    PortfolioReadQueryOptions? Options = null) : IQuery<PortfolioSignalsDto>;
