using PoTool.Core.Domain.EffortPlanning;
using PoTool.Core.Domain.Statistics;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class EffortPlanningCdcServicesTests
{
    private static readonly IEffortDistributionService DistributionService = new EffortDistributionService();
    private static readonly IEffortEstimationQualityService QualityService = new EffortEstimationQualityService();
    private static readonly IEffortEstimationSuggestionService SuggestionService = new EffortEstimationSuggestionService();

    [TestMethod]
    public void EffortDistribution_ProducesCanonicalAreaIterationAndTotalRollups()
    {
        IReadOnlyList<EffortPlanningWorkItem> inputs =
        [
            CreateWorkItem(1, "Task", "A", "Area1", "Sprint 2", "Done", 5),
            CreateWorkItem(2, "Task", "B", "Area1", "Sprint 2", "Done", 8),
            CreateWorkItem(3, "Task", "C", "Area2", "Sprint 1", "Done", 3),
            CreateWorkItem(4, "Task", "D", "Area1", "Sprint 1", "Done", 13)
        ];
        var result = DistributionService.Analyze(
            inputs,
            maxIterations: 10,
            defaultCapacityPerIteration: 20);
        var totalWorkItemEffort = inputs.Sum(static item => item.Effort ?? 0);

        Assert.AreEqual(totalWorkItemEffort, result.TotalEffort);
        Assert.AreEqual(totalWorkItemEffort, result.EffortByArea.Sum(static area => area.TotalEffort));
        Assert.AreEqual(totalWorkItemEffort, result.EffortByIteration.Sum(static iteration => iteration.TotalEffort));
        Assert.AreEqual(26, result.EffortByArea.Single(area => area.AreaPath == "Area1").TotalEffort);
        Assert.AreEqual(13, result.EffortByIteration.Single(iteration => iteration.IterationPath == "Sprint 2").TotalEffort);
        Assert.AreEqual(65d, result.EffortByIteration.Single(iteration => iteration.IterationPath == "Sprint 2").UtilizationPercentage, 0.001d);
    }

    [TestMethod]
    public void EffortDistribution_ComputesHeatMapCapacityStatuses()
    {
        var result = DistributionService.Analyze(
        [
            CreateWorkItem(1, "Task", "A", "Area1", "Sprint 1", "Done", 20),
            CreateWorkItem(2, "Task", "B", "Area2", "Sprint 1", "Done", 35),
            CreateWorkItem(3, "Task", "C", "Area3", "Sprint 1", "Done", 45),
            CreateWorkItem(4, "Task", "D", "Area4", "Sprint 1", "Done", 55)
        ],
        maxIterations: 10,
        defaultCapacityPerIteration: 50);

        Assert.AreEqual(CapacityStatus.Underutilized, result.HeatMapData.Single(cell => cell.AreaPath == "Area1").Status);
        Assert.AreEqual(CapacityStatus.Normal, result.HeatMapData.Single(cell => cell.AreaPath == "Area2").Status);
        Assert.AreEqual(CapacityStatus.NearCapacity, result.HeatMapData.Single(cell => cell.AreaPath == "Area3").Status);
        Assert.AreEqual(CapacityStatus.OverCapacity, result.HeatMapData.Single(cell => cell.AreaPath == "Area4").Status);
    }

    [TestMethod]
    public void EffortEstimationQuality_UsesStatisticsMathForAccuracyCalculations()
    {
        IReadOnlyList<EffortPlanningWorkItem> inputs =
        [
            CreateWorkItem(1, "Task", "A", "Area", "Sprint 1", "Done", 2, DateTimeOffset.UnixEpoch),
            CreateWorkItem(2, "Task", "B", "Area", "Sprint 1", "Done", 4, DateTimeOffset.UnixEpoch.AddDays(1)),
            CreateWorkItem(3, "Task", "C", "Area", "Sprint 2", "Done", 8, DateTimeOffset.UnixEpoch.AddDays(2))
        ];

        var result = QualityService.Analyze(inputs, maxIterations: 10);
        var taskQuality = result.QualityByType.Single();

        var expectedVariance = StatisticsMath.Variance(new[] { 2d, 4d, 8d });
        var expectedTypeAccuracy = 1d - Math.Min(1d, Math.Sqrt(expectedVariance) / 5d);
        var expectedOverallAccuracy = 1d - Math.Min(1d, Math.Sqrt(expectedVariance) / (14d / 3d));

        Assert.AreEqual(expectedTypeAccuracy, taskQuality.AverageAccuracy, 0.0001d);
        Assert.AreEqual(expectedOverallAccuracy, result.AverageEstimationAccuracy, 0.0001d);
        Assert.AreEqual("Sprint 1", result.TrendOverTime[0].Period);
        Assert.AreEqual("Sprint 2", result.TrendOverTime[1].Period);
    }

    [TestMethod]
    public void EffortEstimationSuggestions_UsesMedianSelectionAndSimilarityRanking()
    {
        var target = CreateWorkItem(10, "Task", "Implement task api", "Area\\A", "Sprint 3", "In Progress", null);
        IReadOnlyList<EffortPlanningWorkItem> history =
        [
            CreateWorkItem(1, "Task", "Implement task api endpoint", "Area\\A", "Sprint 1", "Done", 3),
            CreateWorkItem(2, "Task", "Refactor backlog import workflow", "Area\\B", "Sprint 1", "Done", 5),
            CreateWorkItem(3, "Task", "Legacy reporting cleanup", "Area\\C", "Sprint 2", "Done", 8),
            CreateWorkItem(4, "Bug", "Fix task api bug", "Area\\D", "Sprint 2", "Done", 13)
        ];

        var result = SuggestionService.GenerateSuggestion(target, history, EffortEstimationSettingsDto.Default);
        var similarityScores = result.SimilarWorkItems.Select(item => item.SimilarityScore).ToList();

        Assert.AreEqual(5, result.SuggestedEffort);
        Assert.AreEqual(3, result.HistoricalMatchCount);
        Assert.AreEqual(3, result.HistoricalEffortMin);
        Assert.AreEqual(8, result.HistoricalEffortMax);
        Assert.HasCount(3, result.SimilarWorkItems);
        Assert.AreEqual(1, result.SimilarWorkItems[0].WorkItemId);
        CollectionAssert.AreEqual(similarityScores.OrderByDescending(score => score).ToList(), similarityScores);
    }

    [TestMethod]
    public void EffortEstimationSuggestions_UsesConfiguredDefaultWhenNoHistoricalTypeMatches()
    {
        var result = SuggestionService.GenerateSuggestion(
            CreateWorkItem(11, "Epic", "Roadmap epic", "Area\\A", "Sprint 3", "New", null),
            [
                CreateWorkItem(1, "Task", "Task history", "Area\\A", "Sprint 1", "Done", 3)
            ],
            EffortEstimationSettingsDto.Default);

        Assert.AreEqual(EffortEstimationSettingsDto.Default.DefaultEffortEpic, result.SuggestedEffort);
        Assert.AreEqual(0.3d, result.Confidence, 0.0001d);
        Assert.AreEqual(0, result.HistoricalMatchCount);
        Assert.AreEqual(EffortEstimationSettingsDto.Default.DefaultEffortEpic, result.HistoricalEffortMin);
        Assert.AreEqual(EffortEstimationSettingsDto.Default.DefaultEffortEpic, result.HistoricalEffortMax);
    }

    private static EffortPlanningWorkItem CreateWorkItem(
        int workItemId,
        string workItemType,
        string title,
        string areaPath,
        string iterationPath,
        string state,
        int? effort,
        DateTimeOffset? retrievedAt = null)
    {
        return new EffortPlanningWorkItem(
            workItemId,
            workItemType,
            title,
            areaPath,
            iterationPath,
            state,
            retrievedAt ?? DateTimeOffset.UtcNow,
            effort);
    }
}
