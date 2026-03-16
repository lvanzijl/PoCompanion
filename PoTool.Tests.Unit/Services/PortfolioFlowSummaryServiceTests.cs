using PoTool.Core.Domain.Portfolio;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioFlowSummaryServiceTests
{
    [TestMethod]
    public void BuildTrend_ComputesCanonicalSprintAndRangeRollups()
    {
        var service = new PortfolioFlowSummaryService();

        var result = service.BuildTrend(
            new PortfolioFlowTrendRequest(
                [
                    new PortfolioFlowSprintInfo(1, "Sprint 1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(13)),
                    new PortfolioFlowSprintInfo(2, "Sprint 2", DateTimeOffset.UnixEpoch.AddDays(14), DateTimeOffset.UnixEpoch.AddDays(27)),
                    new PortfolioFlowSprintInfo(3, "Sprint 3", DateTimeOffset.UnixEpoch.AddDays(28), DateTimeOffset.UnixEpoch.AddDays(41))
                ],
                [
                    new PortfolioFlowProjectionInput(1, 10, 10, 7, 2, 1),
                    new PortfolioFlowProjectionInput(1, 11, 20, 8, 1, 4),
                    new PortfolioFlowProjectionInput(2, 10, 12, 5, 1, 5),
                    new PortfolioFlowProjectionInput(2, 11, 18, 3, 2, 4)
                ]));

        Assert.HasCount(3, result.Sprints);
        Assert.IsTrue(result.Sprints[0].HasData);
        Assert.AreEqual(30d, result.Sprints[0].StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(15d, result.Sprints[0].RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(3d, result.Sprints[0].InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(5d, result.Sprints[0].ThroughputStoryPoints!.Value, 0.001d);
        Assert.AreEqual(50d, result.Sprints[0].CompletionPercent!.Value, 0.001d);
        Assert.AreEqual(2d, result.Sprints[0].NetFlowStoryPoints!.Value, 0.001d);

        Assert.IsTrue(result.Sprints[1].HasData);
        Assert.AreEqual(30d, result.Sprints[1].StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(8d, result.Sprints[1].RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(3d, result.Sprints[1].InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(9d, result.Sprints[1].ThroughputStoryPoints!.Value, 0.001d);
        Assert.AreEqual(73.333d, result.Sprints[1].CompletionPercent!.Value, 0.001d);
        Assert.AreEqual(6d, result.Sprints[1].NetFlowStoryPoints!.Value, 0.001d);

        Assert.IsFalse(result.Sprints[2].HasData);
        Assert.IsNull(result.Sprints[2].StockStoryPoints);
        Assert.IsNull(result.Sprints[2].CompletionPercent);

        Assert.AreEqual(8d, result.Summary.CumulativeNetFlowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(0d, result.Summary.TotalScopeChangeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(-7d, result.Summary.RemainingScopeChangeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(0d, result.Summary.TotalScopeChangePercent!.Value, 0.001d);
        Assert.AreEqual(PortfolioTrajectory.Contracting, result.Summary.Trajectory);
    }
}
