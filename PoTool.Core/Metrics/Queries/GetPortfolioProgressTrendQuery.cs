using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to compute portfolio progress trend metrics across a selected sprint range.
/// </summary>
/// <param name="ProductOwnerId">Product Owner (Profile) ID — scopes all data to this owner's products.</param>
/// <param name="SprintIds">Sprint IDs to include in the trend, ordered chronologically.</param>
/// <param name="ProductIds">
///   Optional product filter. When null or empty, all products owned by the ProductOwner
///   are aggregated (default = All Products). When populated, only those products are used.
/// </param>
public sealed record GetPortfolioProgressTrendQuery(
    int ProductOwnerId,
    IReadOnlyList<int> SprintIds,
    int[]? ProductIds = null
) : IQuery<PortfolioProgressTrendDto>;
