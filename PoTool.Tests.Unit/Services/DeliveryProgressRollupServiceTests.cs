using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class DeliveryProgressRollupServiceTests
{
    [TestMethod]
    public void ComputeFeatureProgress_CalculatesFeatureRollupAndSprintProgressChange()
    {
        var service = CreateService();

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Done PBI", parentId: 100, state: "Done", storyPoints: 5),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, "Active PBI", parentId: 100, state: "Active", storyPoints: 10)
            },
            [1],
            FeatureProgressMode.StoryPoints,
            SprintCompletedPbiIds: new HashSet<int> { 201 }));

        Assert.HasCount(1, result);
        Assert.AreEqual(33, result[0].ProgressPercent);
        Assert.AreEqual(15d, result[0].TotalScopeStoryPoints, 0.001d);
        Assert.AreEqual(5d, result[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(5d, result[0].SprintDeliveredStoryPoints, 0.001d);
        Assert.AreEqual(33.33d, result[0].SprintProgressionDelta.Percentage, 0.001d);
    }

    [TestMethod]
    public void ComputeFeatureProgress_KeepsSprintAssignedFeatureVisibleWithoutActivity()
    {
        var service = CreateService();

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100, resolvedSprintId: 7)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Assigned PBI", parentId: 100, state: "Active", storyPoints: 8)
            },
            [1],
            FeatureProgressMode.StoryPoints,
            ActiveWorkItemIds: Array.Empty<int>(),
            SprintAssignedPbiIds: new HashSet<int> { 201 }));

        Assert.HasCount(1, result, "Sprint-assigned PBIs should keep the parent feature visible even without activity.");
        Assert.AreEqual(100, result[0].FeatureId);
    }

    [TestMethod]
    public void ComputeFeatureProgress_UsesStrictOverridePercentForFeatureWithoutChildren()
    {
        var service = CreateService();

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active", timeCriticality: 50)
            },
            [1],
            FeatureProgressMode.StoryPoints));

        Assert.HasCount(1, result);
        Assert.AreEqual(0d, result[0].CalculatedProgress!.Value, 0.001d);
        Assert.AreEqual(50d, result[0].Override!.Value, 0.001d);
        Assert.AreEqual(50d, result[0].EffectiveProgress!.Value, 0.001d);
        Assert.AreEqual(0d, result[0].Weight, 0.001d);
        Assert.IsTrue(result[0].IsExcluded);
    }

    [TestMethod]
    public void ComputeFeatureProgress_DelegatesForecastCalculationToFeatureForecastService()
    {
        var service = CreateService(featureForecastService: new StubFeatureForecastService(
            new FeatureForecastResult(
                Effort: 100d,
                EffectiveProgress: 50d,
                ForecastConsumedEffort: 12.34d,
                ForecastRemainingEffort: 56.78d)));

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active", effort: 100),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Done PBI", parentId: 100, state: "Done", storyPoints: 5)
            },
            [1],
            FeatureProgressMode.StoryPoints));

        Assert.HasCount(1, result);
        Assert.AreEqual(100d, result[0].Effort!.Value, 0.001d);
        Assert.AreEqual(12.34d, result[0].ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(56.78d, result[0].ForecastRemainingEffort!.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeFeatureProgress_ComputesFeatureForecastFromEffectiveProgressAndFeatureEffort()
    {
        var service = CreateService();

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active", effort: 80),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Done PBI", parentId: 100, state: "Done", storyPoints: 5),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, "Active PBI", parentId: 100, state: "Active", storyPoints: 5)
            },
            [1],
            FeatureProgressMode.StoryPoints));

        Assert.HasCount(1, result);
        Assert.AreEqual(50d, result[0].EffectiveProgress!.Value, 0.001d);
        Assert.AreEqual(40d, result[0].ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(40d, result[0].ForecastRemainingEffort!.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeEpicProgress_AggregatesFeatureRollups()
    {
        var service = CreateService();

        var result = service.ComputeEpicProgress(new DeliveryEpicProgressRequest(
            [
                new FeatureProgress(200, "Feature A", 1, 100, "Epic X", 50, 40, 20, 2, false, 10, new ProgressionDelta(25), 4, 1, false, effectiveProgress: 50, forecastConsumedEffort: 20, forecastRemainingEffort: 80, weight: 2),
                new FeatureProgress(201, "Feature B", 1, 100, "Epic X", 80, 60, 48, 3, false, 20, new ProgressionDelta(33.33), 6, 2, true, effectiveProgress: 100, forecastConsumedEffort: 30, forecastRemainingEffort: 70, weight: 1)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, "Epic", "Epic X", state: "Active")
            }));

        Assert.HasCount(1, result);
        Assert.AreEqual(100, result[0].TotalScopeStoryPoints, 0.001d);
        Assert.AreEqual(68, result[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(66.67d, result[0].AggregatedProgress!.Value, 0.01d);
        Assert.AreEqual(50d, result[0].ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(150d, result[0].ForecastRemainingEffort!.Value, 0.001d);
        Assert.AreEqual(0, result[0].ExcludedFeaturesCount);
        Assert.AreEqual(30d, result[0].SprintDeliveredStoryPoints, 0.001d);
        Assert.AreEqual(30d, result[0].SprintProgressionDelta.Percentage, 0.001d);
        Assert.AreEqual(10, result[0].SprintEffortDelta);
        Assert.AreEqual(1, result[0].SprintCompletedFeatureCount);
    }

    [TestMethod]
    public void ComputeEpicProgress_DelegatesCanonicalAggregationToEpicAggregationService()
    {
        var service = CreateService(epicAggregationService: new StubEpicAggregationService(
            new EpicAggregationResult(12.5d, 1.25d, 8.75d, 3, 2, 5)));

        var result = service.ComputeEpicProgress(new DeliveryEpicProgressRequest(
            [
                new FeatureProgress(200, "Feature A", 1, 100, "Epic X", 50, 40, 20, 2, false, 10, new ProgressionDelta(25), 4, 1, false, effectiveProgress: 50, weight: 2)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, "Epic", "Epic X", state: "Active")
            }));

        Assert.HasCount(1, result);
        Assert.AreEqual(12.5d, result[0].AggregatedProgress!.Value, 0.001d);
        Assert.AreEqual(13, result[0].ProgressPercent);
        Assert.AreEqual(1.25d, result[0].ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(8.75d, result[0].ForecastRemainingEffort!.Value, 0.001d);
        Assert.AreEqual(3, result[0].ExcludedFeaturesCount);
        Assert.AreEqual(2, result[0].IncludedFeaturesCount);
        Assert.AreEqual(5d, result[0].TotalWeight, 0.001d);
    }

    [TestMethod]
    public void ComputeEpicProgress_PreservesNullProgress_WhenAggregationIsUnknown()
    {
        var service = CreateService(epicAggregationService: new StubEpicAggregationService(
            new EpicAggregationResult(null, 1.25d, 8.75d, 3, 0, 0)));

        var result = service.ComputeEpicProgress(new DeliveryEpicProgressRequest(
            [
                new FeatureProgress(200, "Feature A", 1, 100, "Epic X", 0, 0, 0, 0, false, 0, new ProgressionDelta(0), 0, 0, false, weight: 0, isExcluded: true)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, "Epic", "Epic X", state: "Active")
            }));

        Assert.HasCount(1, result);
        Assert.IsNull(result[0].AggregatedProgress);
        Assert.IsNull(result[0].ProgressPercent, "Unknown epic progress must stay null and must not be coerced to zero.");
        Assert.AreEqual(1.25d, result[0].ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(8.75d, result[0].ForecastRemainingEffort!.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeProductSummaries_AggregatesEpicOutputsByProduct()
    {
        var result = DeliveryProgressSummaryCalculator.ComputeProductSummaries(
        [
            new EpicProgress(100, "Epic X", 1, 50, 40, 20, 2, 1, 2, false, 10, new ProgressionDelta(25), 4, 1, 1),
            new EpicProgress(101, "Epic Y", 1, 70, 60, 42, 3, 2, 4, false, 12, new ProgressionDelta(20), -1, 2, 0),
            new EpicProgress(200, "Epic Z", 2, 80, 50, 40, 2, 1, 3, false, 8, new ProgressionDelta(16), 6, 2, 2)
        ]);

        Assert.HasCount(2, result);
        Assert.IsTrue(result.ContainsKey(1));
        Assert.IsTrue(result.ContainsKey(2));
        Assert.AreEqual(3, result[1].ScopeChangeEffort);
        Assert.AreEqual(1, result[1].CompletedFeatureCount);
        Assert.AreEqual(6, result[2].ScopeChangeEffort);
        Assert.AreEqual(2, result[2].CompletedFeatureCount);
    }

    [TestMethod]
    public void ComputeProgressionDelta_ReturnsZero_WhenNoFeaturesExist()
    {
        var service = CreateService();

        var result = service.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
            Array.Empty<DeliveryTrendResolvedWorkItem>(),
            new Dictionary<int, DeliveryTrendWorkItem>(),
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>()));

        Assert.AreEqual(0d, result.Percentage, 0.001d);
    }

    [TestMethod]
    public void ComputeProgressionDelta_AveragesOnlyFeaturesWithStateProgress()
    {
        var service = CreateService();

        var result = service.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(101, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100),
                CreateResolvedWorkItem(301, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 101),
                CreateResolvedWorkItem(302, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 101)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [101] = CreateWorkItem(101, CanonicalWorkItemTypes.Feature, "Feature B", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Feature A Done", parentId: 100, state: "Done", storyPoints: 5),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, "Feature A Active", parentId: 100, state: "Active", storyPoints: 5),
                [301] = CreateWorkItem(301, CanonicalWorkItemTypes.ProductBacklogItem, "Feature B Done", parentId: 101, state: "Done", storyPoints: 5),
                [302] = CreateWorkItem(302, CanonicalWorkItemTypes.ProductBacklogItem, "Feature B Active", parentId: 101, state: "Active", storyPoints: 5)
            },
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
            {
                [201] = [CreateFieldChangeEvent(201, "System.State", DateTimeOffset.UtcNow, "Active", "Done")]
            }));

        Assert.AreEqual(50d, result.Percentage, 0.001d);
    }

    private static IDeliveryProgressRollupService CreateService(
        IFeatureForecastService? featureForecastService = null,
        IEpicAggregationService? epicAggregationService = null)
    {
        var storyPointResolutionService = new CanonicalStoryPointResolutionService();
        var hierarchyRollupService = new HierarchyRollupService(storyPointResolutionService);
        return new DeliveryProgressRollupService(
            storyPointResolutionService,
            hierarchyRollupService,
            featureForecastService: featureForecastService,
            epicAggregationService: epicAggregationService);
    }

    private sealed class StubFeatureForecastService(FeatureForecastResult result) : IFeatureForecastService
    {
        public FeatureForecastResult Compute(FeatureForecastCalculationRequest request) => result;
    }

    private sealed class StubEpicAggregationService(EpicAggregationResult result) : IEpicAggregationService
    {
        public EpicAggregationResult Compute(EpicAggregationRequest request) => result;
    }

    private static DeliveryTrendResolvedWorkItem CreateResolvedWorkItem(
        int workItemId,
        string workItemType,
        int? resolvedFeatureId = null,
        int? resolvedEpicId = null,
        int? resolvedProductId = 1,
        int? resolvedSprintId = 1)
    {
        return new DeliveryTrendResolvedWorkItem(
            workItemId,
            workItemType,
            resolvedProductId,
            resolvedFeatureId,
            resolvedEpicId,
            resolvedSprintId);
    }

    private static DeliveryTrendWorkItem CreateWorkItem(
        int workItemId,
        string workItemType,
        string title,
        int? parentId = null,
        string? state = "New",
        string? iterationPath = "\\Project\\Sprint 1",
        int? effort = null,
        int? storyPoints = null,
        int? businessValue = null,
        DateTimeOffset? createdDate = null,
        double? timeCriticality = null)
    {
        effort ??= storyPoints;

        return new DeliveryTrendWorkItem(
            workItemId,
            workItemType,
            title,
            parentId,
            state,
            iterationPath,
            effort,
            storyPoints,
            businessValue,
            createdDate,
            timeCriticality);
    }

    private static FieldChangeEvent CreateFieldChangeEvent(
        int workItemId,
        string fieldRefName,
        DateTimeOffset timestamp,
        string? oldValue,
        string? newValue)
    {
        return new FieldChangeEvent(
            1,
            workItemId,
            1,
            fieldRefName,
            timestamp,
            timestamp.UtcDateTime,
            oldValue,
            newValue);
    }
}
