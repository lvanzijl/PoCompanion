using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to read the latest available portfolio snapshot.
/// </summary>
public sealed record GetPortfolioSnapshotsQuery(
    int ProductOwnerId,
    PortfolioReadQueryOptions? Options = null) : IQuery<PortfolioSnapshotDto>;
