using PoTool.Shared.Metrics;

namespace PoTool.Core.Domain.Portfolio;

/// <summary>
/// Produces canonical portfolio stock-and-flow rollups from preloaded PortfolioFlow projection inputs.
/// </summary>
public interface IPortfolioFlowSummaryService
{
    /// <summary>
    /// Builds per-sprint and multi-sprint portfolio flow summaries from canonical projection rows.
    /// </summary>
    PortfolioFlowTrendResult BuildTrend(PortfolioFlowTrendRequest request);
}

/// <summary>
/// Ordered sprint metadata and product-level projection rows used to build portfolio flow summaries.
/// </summary>
public sealed record PortfolioFlowTrendRequest(
    IReadOnlyList<PortfolioFlowSprintInfo> Sprints,
    IReadOnlyList<PortfolioFlowProjectionInput> Projections);

/// <summary>
/// Sprint metadata required for portfolio trend shaping.
/// </summary>
public sealed record PortfolioFlowSprintInfo(
    int SprintId,
    string SprintName,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc);

/// <summary>
/// Canonical product-level PortfolioFlow projection row supplied by an application adapter.
/// </summary>
public sealed record PortfolioFlowProjectionInput(
    int SprintId,
    int ProductId,
    double StockStoryPoints,
    double RemainingScopeStoryPoints,
    double InflowStoryPoints,
    double ThroughputStoryPoints);

/// <summary>
/// Canonical portfolio flow rollup for a single sprint.
/// </summary>
public sealed record PortfolioFlowSummaryResult(
    int SprintId,
    double? StockStoryPoints,
    double? RemainingScopeStoryPoints,
    double? InflowStoryPoints,
    double? ThroughputStoryPoints,
    double? CompletionPercent,
    double? NetFlowStoryPoints,
    bool HasData);

/// <summary>
/// Canonical portfolio trend summary across the selected sprint range.
/// </summary>
public sealed record PortfolioFlowTrendSummaryResult(
    double? CumulativeNetFlowStoryPoints,
    double? TotalScopeChangeStoryPoints,
    double? TotalScopeChangePercent,
    double? RemainingScopeChangeStoryPoints,
    PortfolioTrajectory Trajectory);

/// <summary>
/// Canonical portfolio flow rollups for all selected sprints plus the range summary.
/// </summary>
public sealed record PortfolioFlowTrendResult(
    IReadOnlyList<PortfolioFlowSummaryResult> Sprints,
    PortfolioFlowTrendSummaryResult Summary);

/// <summary>
/// Implements canonical portfolio flow summary formulas inside the PortfolioFlow CDC slice.
/// </summary>
public sealed class PortfolioFlowSummaryService : IPortfolioFlowSummaryService
{
    private const double TrajectoryToleranceStoryPoints = 3.0;

    /// <inheritdoc />
    public PortfolioFlowTrendResult BuildTrend(PortfolioFlowTrendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var projectionsBySprint = request.Projections
            .GroupBy(projection => projection.SprintId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var sprintResults = request.Sprints
            .Select(sprint =>
            {
                if (!projectionsBySprint.TryGetValue(sprint.SprintId, out var sprintProjections))
                {
                    return new PortfolioFlowSummaryResult(
                        sprint.SprintId,
                        StockStoryPoints: null,
                        RemainingScopeStoryPoints: null,
                        InflowStoryPoints: null,
                        ThroughputStoryPoints: null,
                        CompletionPercent: null,
                        NetFlowStoryPoints: null,
                        HasData: false);
                }

                var stockStoryPoints = sprintProjections.Sum(projection => projection.StockStoryPoints);
                var remainingScopeStoryPoints = sprintProjections.Sum(projection => projection.RemainingScopeStoryPoints);
                var inflowStoryPoints = sprintProjections.Sum(projection => projection.InflowStoryPoints);
                var throughputStoryPoints = sprintProjections.Sum(projection => projection.ThroughputStoryPoints);

                return new PortfolioFlowSummaryResult(
                    sprint.SprintId,
                    stockStoryPoints,
                    remainingScopeStoryPoints,
                    inflowStoryPoints,
                    throughputStoryPoints,
                    CompletionPercent: stockStoryPoints > 0d
                        ? (stockStoryPoints - remainingScopeStoryPoints) / stockStoryPoints * 100d
                        : null,
                    NetFlowStoryPoints: throughputStoryPoints - inflowStoryPoints,
                    HasData: true);
            })
            .ToList();

        var sprintsWithData = sprintResults.Where(sprint => sprint.HasData).ToList();
        if (sprintsWithData.Count == 0)
        {
            return new PortfolioFlowTrendResult(
                sprintResults,
                new PortfolioFlowTrendSummaryResult(
                    CumulativeNetFlowStoryPoints: null,
                    TotalScopeChangeStoryPoints: null,
                    TotalScopeChangePercent: null,
                    RemainingScopeChangeStoryPoints: null,
                    Trajectory: PortfolioTrajectory.Stable));
        }

        var firstSprint = sprintsWithData[0];
        var lastSprint = sprintsWithData[^1];
        var cumulativeNetFlowStoryPoints = sprintsWithData.Sum(sprint => sprint.NetFlowStoryPoints ?? 0d);
        double? totalScopeChangeStoryPoints =
            firstSprint.StockStoryPoints.HasValue && lastSprint.StockStoryPoints.HasValue
                ? lastSprint.StockStoryPoints.Value - firstSprint.StockStoryPoints.Value
                : null;
        double? totalScopeChangePercent =
            totalScopeChangeStoryPoints.HasValue
            && firstSprint.StockStoryPoints.HasValue
            && firstSprint.StockStoryPoints.Value > 0d
                ? totalScopeChangeStoryPoints.Value / firstSprint.StockStoryPoints.Value * 100d
                : null;
        double? remainingScopeChangeStoryPoints =
            firstSprint.RemainingScopeStoryPoints.HasValue && lastSprint.RemainingScopeStoryPoints.HasValue
                ? lastSprint.RemainingScopeStoryPoints.Value - firstSprint.RemainingScopeStoryPoints.Value
                : null;

        var trajectory = cumulativeNetFlowStoryPoints switch
        {
            > TrajectoryToleranceStoryPoints => PortfolioTrajectory.Contracting,
            < -TrajectoryToleranceStoryPoints => PortfolioTrajectory.Expanding,
            _ => PortfolioTrajectory.Stable
        };

        return new PortfolioFlowTrendResult(
            sprintResults,
            new PortfolioFlowTrendSummaryResult(
                cumulativeNetFlowStoryPoints,
                totalScopeChangeStoryPoints,
                totalScopeChangePercent,
                remainingScopeChangeStoryPoints,
                trajectory));
    }
}
