using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class DeliveryTrendDomainModelsTests
{
    [TestMethod]
    public void SprintDeliveryProjection_UsesCanonicalStoryPointProperties()
    {
        var projection = CreateProjection(
            sprintId: 11,
            productId: 7,
            plannedStoryPoints: 21.5,
            completedPbiStoryPoints: 8.5,
            spilloverStoryPoints: 5.5,
            derivedStoryPoints: 3.5,
            progressionPercentage: 25);

        Assert.AreEqual(11, projection.SprintId);
        Assert.AreEqual(7, projection.ProductId);
        Assert.AreEqual(21.5d, projection.PlannedStoryPoints);
        Assert.AreEqual(8.5d, projection.CompletedPbiStoryPoints);
        Assert.AreEqual(5.5d, projection.SpilloverStoryPoints);
        Assert.AreEqual(3.5d, projection.DerivedStoryPoints);
        Assert.AreEqual(25d, projection.ProgressionDelta.Percentage);
    }

    [TestMethod]
    public void SprintTrendMetrics_AggregatesTotalsFromProductProjections()
    {
        var metrics = new SprintTrendMetrics(
            42,
            "Sprint 42",
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero),
            [
                CreateProjection(
                    sprintId: 42,
                    productId: 1,
                    plannedCount: 3,
                    plannedEffort: 10,
                    plannedStoryPoints: 13,
                    workedCount: 2,
                    workedEffort: 8,
                    completedPbiCount: 1,
                    completedPbiEffort: 3,
                    completedPbiStoryPoints: 5,
                    spilloverCount: 1,
                    spilloverEffort: 2,
                    spilloverStoryPoints: 3,
                    bugsCreatedCount: 1,
                    derivedStoryPointCount: 1,
                    derivedStoryPoints: 2,
                    unestimatedDeliveryCount: 1,
                    progressionPercentage: 20),
                CreateProjection(
                    sprintId: 42,
                    productId: 2,
                    plannedCount: 4,
                    plannedEffort: 12,
                    plannedStoryPoints: 21,
                    workedCount: 5,
                    workedEffort: 14,
                    bugsPlannedCount: 1,
                    bugsWorkedCount: 2,
                    completedPbiCount: 2,
                    completedPbiEffort: 7,
                    completedPbiStoryPoints: 8,
                    spilloverCount: 2,
                    spilloverEffort: 6,
                    spilloverStoryPoints: 5,
                    bugsClosedCount: 1,
                    missingEffortCount: 1,
                    missingStoryPointCount: 2,
                    derivedStoryPointCount: 2,
                    derivedStoryPoints: 4,
                    unestimatedDeliveryCount: 2,
                    isApproximate: true,
                    progressionPercentage: 15)
            ]);

        Assert.HasCount(2, metrics.ProductProjections);
        Assert.AreEqual(7, metrics.TotalPlannedCount);
        Assert.AreEqual(22, metrics.TotalPlannedEffort);
        Assert.AreEqual(34d, metrics.TotalPlannedStoryPoints);
        Assert.AreEqual(7, metrics.TotalWorkedCount);
        Assert.AreEqual(22, metrics.TotalWorkedEffort);
        Assert.AreEqual(1, metrics.TotalBugsPlannedCount);
        Assert.AreEqual(2, metrics.TotalBugsWorkedCount);
        Assert.AreEqual(3, metrics.TotalCompletedPbiCount);
        Assert.AreEqual(10, metrics.TotalCompletedPbiEffort);
        Assert.AreEqual(13d, metrics.TotalCompletedPbiStoryPoints);
        Assert.AreEqual(3, metrics.TotalSpilloverCount);
        Assert.AreEqual(8, metrics.TotalSpilloverEffort);
        Assert.AreEqual(8d, metrics.TotalSpilloverStoryPoints);
        Assert.AreEqual(35d, metrics.TotalProgressionDelta);
        Assert.AreEqual(1, metrics.TotalBugsCreatedCount);
        Assert.AreEqual(1, metrics.TotalBugsClosedCount);
        Assert.AreEqual(1, metrics.TotalMissingEffortCount);
        Assert.AreEqual(2, metrics.TotalMissingStoryPointCount);
        Assert.AreEqual(3, metrics.TotalDerivedStoryPointCount);
        Assert.AreEqual(6d, metrics.TotalDerivedStoryPoints);
        Assert.AreEqual(3, metrics.TotalUnestimatedDeliveryCount);
        Assert.IsTrue(metrics.IsApproximate);
    }

    [TestMethod]
    public void FeatureAndEpicProgress_PreserveStoryPointUnitsAndTrueEffortDelta()
    {
        var featureProgress = new FeatureProgress(
            1001,
            "Feature A",
            7,
            500,
            "Epic A",
            60,
            21.5,
            13.5,
            3,
            false,
            5.5,
            new ProgressionDelta(25.58),
            -8,
            2,
            true);

        var epicProgress = new EpicProgress(
            500,
            "Epic A",
            7,
            70,
            55.5,
            34.5,
            4,
            2,
            6,
            false,
            8.5,
            new ProgressionDelta(15.32),
            12,
            3,
            1);

        Assert.AreEqual(21.5d, featureProgress.TotalScopeStoryPoints);
        Assert.AreEqual(13.5d, featureProgress.DeliveredStoryPoints);
        Assert.AreEqual(5.5d, featureProgress.SprintDeliveredStoryPoints);
        Assert.AreEqual(-8, featureProgress.SprintEffortDelta);
        Assert.AreEqual(25.58d, featureProgress.SprintProgressionDelta.Percentage);

        Assert.AreEqual(55.5d, epicProgress.TotalScopeStoryPoints);
        Assert.AreEqual(34.5d, epicProgress.DeliveredStoryPoints);
        Assert.AreEqual(8.5d, epicProgress.SprintDeliveredStoryPoints);
        Assert.AreEqual(12, epicProgress.SprintEffortDelta);
        Assert.AreEqual(15.32d, epicProgress.SprintProgressionDelta.Percentage);
    }

    [TestMethod]
    public void ProductDeliveryProgressSummary_PreservesProductLevelSprintDiagnostics()
    {
        var summary = new ProductDeliveryProgressSummary(7, -3, 2);

        Assert.AreEqual(7, summary.ProductId);
        Assert.AreEqual(-3, summary.ScopeChangeEffort);
        Assert.AreEqual(2, summary.CompletedFeatureCount);
    }

    [TestMethod]
    public void DeliveryTrendModels_RejectInvalidConstructionValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProgressionDelta(101));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateProjection(sprintId: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateProjection(plannedStoryPoints: -1));
        Assert.ThrowsExactly<ArgumentException>(() => new SprintTrendMetrics(1, " ", null, null, Array.Empty<SprintDeliveryProjection>()));
        Assert.ThrowsExactly<ArgumentException>(() => new FeatureProgress(1, "Feature A", 1, 10, null, 50, 5, 2, 1, false, 1, new ProgressionDelta(10), 0, 1, false));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new EpicProgress(1, "Epic A", 1, 101, 5, 2, 1, 1, 1, true, 1, new ProgressionDelta(5), 0, 1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProductDeliveryProgressSummary(0, 1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProductDeliveryProgressSummary(1, 1, -1));
    }

    [TestMethod]
    public void SprintTrendMetrics_RejectsEndDateBeforeStartDate()
    {
        var startUtc = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero);
        var endUtc = startUtc.AddDays(-1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SprintTrendMetrics(42, "Sprint 42", startUtc, endUtc, [CreateProjection(sprintId: 42)]));
    }

    [TestMethod]
    public void FeatureProgress_RejectsEpicTitleWithoutEpicId()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new FeatureProgress(
                1,
                "Feature A",
                1,
                null,
                "Epic A",
                50,
                5,
                2,
                1,
                false,
                1,
                new ProgressionDelta(10),
                0,
                1,
                false));
    }

    private static SprintDeliveryProjection CreateProjection(
        int sprintId = 1,
        int productId = 1,
        int plannedCount = 1,
        int plannedEffort = 1,
        double plannedStoryPoints = 1,
        int workedCount = 1,
        int workedEffort = 1,
        int bugsPlannedCount = 0,
        int bugsWorkedCount = 0,
        int completedPbiCount = 0,
        int completedPbiEffort = 0,
        double completedPbiStoryPoints = 0,
        int spilloverCount = 0,
        int spilloverEffort = 0,
        double spilloverStoryPoints = 0,
        int bugsCreatedCount = 0,
        int bugsClosedCount = 0,
        int missingEffortCount = 0,
        int missingStoryPointCount = 0,
        int derivedStoryPointCount = 0,
        double derivedStoryPoints = 0,
        int unestimatedDeliveryCount = 0,
        bool isApproximate = false,
        double progressionPercentage = 0)
    {
        return new SprintDeliveryProjection(
            sprintId,
            productId,
            plannedCount,
            plannedEffort,
            plannedStoryPoints,
            workedCount,
            workedEffort,
            bugsPlannedCount,
            bugsWorkedCount,
            completedPbiCount,
            completedPbiEffort,
            completedPbiStoryPoints,
            spilloverCount,
            spilloverEffort,
            spilloverStoryPoints,
            new ProgressionDelta(progressionPercentage),
            bugsCreatedCount,
            bugsClosedCount,
            missingEffortCount,
            missingStoryPointCount,
            derivedStoryPointCount,
            derivedStoryPoints,
            unestimatedDeliveryCount,
            isApproximate);
    }
}
