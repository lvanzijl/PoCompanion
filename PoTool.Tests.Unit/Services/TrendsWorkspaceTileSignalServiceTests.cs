using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class TrendsWorkspaceTileSignalServiceTests
{
    [TestMethod]
    public void GetPrTrendSignal_WithTwoUsablePoints_ReturnsFirstAndLastUsableValues()
    {
        var signal = TrendsWorkspaceTileSignalService.GetPrTrendSignal(
        [
            CreatePrSprintMetric(1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), medianTimeToMergeHours: null),
            CreatePrSprintMetric(2, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), medianTimeToMergeHours: 12),
            CreatePrSprintMetric(3, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), medianTimeToMergeHours: 8)
        ]);

        Assert.IsTrue(signal.HasTrendValues);
        Assert.AreEqual(12d, signal.StartValue);
        Assert.AreEqual(8d, signal.EndValue);
        Assert.IsNull(signal.FallbackSignal);
    }

    [TestMethod]
    public void GetPrTrendSignal_WithSingleUsablePoint_ReturnsInsufficientDataFallback()
    {
        var signal = TrendsWorkspaceTileSignalService.GetPrTrendSignal(
        [
            CreatePrSprintMetric(1, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), medianTimeToMergeHours: 10)
        ]);

        Assert.IsFalse(signal.HasTrendValues);
        Assert.IsNotNull(signal.FallbackSignal);
        Assert.AreEqual(TileSignalKind.InsufficientData, signal.FallbackSignal.Kind);
        Assert.AreEqual("Insufficient data", signal.FallbackSignal.Label);
    }

    [TestMethod]
    public void GetBugTrendSignal_UsesMostRecentPeriodsAndKeepsZeroValuesUsable()
    {
        var signal = TrendsWorkspaceTileSignalService.GetBugTrendSignal(
            new Dictionary<string, int>
            {
                ["2026-01"] = 4,
                ["2026-02"] = 0,
                ["2026-03"] = 0
            });

        Assert.IsTrue(signal.HasTrendValues);
        Assert.AreEqual(0d, signal.StartValue);
        Assert.AreEqual(0d, signal.EndValue);
    }

    [TestMethod]
    public void GetPipelineSignal_WithSelectedProductAboveThreshold_ReturnsUnstable()
    {
        var buildQualityPage = new BuildQualityPageDto
        {
            Summary = CreateBuildQualityResult(successRate: 0.95, eligibleBuilds: 20),
            Products =
            [
                new BuildQualityProductDto
                {
                    ProductId = 7,
                    ProductName = "Signal Product",
                    PipelineDefinitionIds = [],
                    RepositoryIds = [],
                    Result = CreateBuildQualityResult(successRate: 0.70, eligibleBuilds: 10)
                }
            ]
        };

        var signal = TrendsWorkspaceTileSignalService.GetPipelineSignal(buildQualityPage, selectedProductId: 7);

        Assert.AreEqual(TileSignalKind.Unstable, signal.Kind);
        Assert.AreEqual("Unstable", signal.Label);
    }

    [TestMethod]
    public void GetPipelineSignal_WithUnknownSuccessRate_ReturnsNoData()
    {
        var buildQualityPage = new BuildQualityPageDto
        {
            Summary = new BuildQualityResultDto
            {
                Metrics = new BuildQualityMetricsDto(),
                Evidence = new BuildQualityEvidenceDto
                {
                    EligibleBuilds = 0,
                    SuccessRateUnknown = true
                }
            },
            Products = []
        };

        var signal = TrendsWorkspaceTileSignalService.GetPipelineSignal(buildQualityPage, selectedProductId: null);

        Assert.AreEqual(TileSignalKind.NoData, signal.Kind);
        Assert.AreEqual("No data", signal.Label);
    }

    private static PrSprintMetricsDto CreatePrSprintMetric(
        int sprintId,
        DateTimeOffset startUtc,
        double? medianTimeToMergeHours)
    {
        return new PrSprintMetricsDto
        {
            SprintId = sprintId,
            SprintName = $"Sprint {sprintId}",
            StartUtc = startUtc,
            EndUtc = startUtc.AddDays(14),
            TotalPrs = 6,
            MedianPrSize = 42,
            PrSizeIsLinesChanged = true,
            MedianTimeToFirstReviewHours = 3,
            MedianTimeToMergeHours = medianTimeToMergeHours,
            P90TimeToMergeHours = medianTimeToMergeHours
        };
    }

    private static BuildQualityResultDto CreateBuildQualityResult(double successRate, int eligibleBuilds)
    {
        return new BuildQualityResultDto
        {
            Metrics = new BuildQualityMetricsDto
            {
                SuccessRate = successRate
            },
            Evidence = new BuildQualityEvidenceDto
            {
                EligibleBuilds = eligibleBuilds,
                SuccessRateUnknown = false
            }
        };
    }
}
