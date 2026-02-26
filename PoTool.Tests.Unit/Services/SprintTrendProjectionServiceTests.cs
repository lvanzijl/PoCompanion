using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SprintTrendProjectionServiceTests
{
    [TestMethod]
    public async Task ComputeProjectionsAsync_ReturnsEmpty_WhenNoSprintIds()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var service = new SprintTrendProjectionService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SprintTrendProjectionService>.Instance);

        var projections = await service.ComputeProjectionsAsync(1, Array.Empty<int>());

        Assert.HasCount(0, projections);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_BasicProgression_CountsCompletedPbis()
    {
        // Arrange: Feature with 2 PBIs, one Done with activity
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 100, state: "Active", parentId: 100),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateStateChangeActivity(201, sprintStart.AddDays(1), "Active", "Done")
            },
        };

        // Act
        var projection = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Assert
        Assert.AreEqual(1, projection.CompletedPbiCount, "One PBI transitioned to Done");
        Assert.AreEqual(50, projection.CompletedPbiEffort, "Completed PBI effort should be 50");
        Assert.IsGreaterThan(0.0, projection.ProgressionDelta, "Progression delta should be positive");
        Assert.AreEqual(0, projection.MissingEffortCount, "No missing effort");
        Assert.IsFalse(projection.IsApproximate, "No approximation needed");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_MissingEffort_SiblingAverageApproximation()
    {
        // Arrange: 3 PBIs, one with missing effort
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 203, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 100, state: "Active", parentId: 100),
            [203] = CreateWorkItem(203, WorkItemType.Pbi, "PBI 3", effort: null, state: "Active", parentId: 100),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateStateChangeActivity(201, sprintStart.AddDays(1), "Active", "Done")
            },
        };

        // Act
        var projection = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Assert
        Assert.AreEqual(1, projection.MissingEffortCount, "One PBI has missing effort");
        Assert.IsTrue(projection.IsApproximate, "Should be flagged as approximate due to sibling-average");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_BugWorkedOn_ChildTaskStateChange()
    {
        // Arrange: Bug with child task that had a state change
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 300, WorkItemType = WorkItemType.Bug, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 400, WorkItemType = WorkItemType.Task, ResolvedProductId = 1, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [300] = CreateWorkItem(300, WorkItemType.Bug, "Bug 1", state: "Active"),
            [400] = CreateWorkItem(400, WorkItemType.Task, "Task 1", state: "Active", parentId: 300),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [400] = new()
            {
                CreateStateChangeActivity(400, sprintStart.AddDays(2), "New", "Active")
            },
        };

        // Act
        var projection = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Assert
        Assert.AreEqual(1, projection.BugsWorkedCount, "Bug should be counted as worked on because child task had state change");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_BugNotWorkedOn_WhenNoChildTaskStateChange()
    {
        // Arrange: Bug without any child task state changes
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 300, WorkItemType = WorkItemType.Bug, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 400, WorkItemType = WorkItemType.Task, ResolvedProductId = 1, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [300] = CreateWorkItem(300, WorkItemType.Bug, "Bug 1", state: "Active"),
            [400] = CreateWorkItem(400, WorkItemType.Task, "Task 1", state: "New", parentId: 300),
        };

        // No activity at all
        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        // Act
        var projection = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Assert
        Assert.AreEqual(0, projection.BugsWorkedCount, "Bug should not be counted as worked on when no child task state change");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_BugMetrics_NewAndClosed()
    {
        // Arrange: Bug created and closed during sprint
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 300, WorkItemType = WorkItemType.Bug, ResolvedProductId = 1, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [300] = CreateWorkItem(300, WorkItemType.Bug, "Bug 1", state: "Done",
                createdDate: sprintStart.AddDays(1)),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [300] = new()
            {
                CreateStateChangeActivity(300, sprintStart.AddDays(3), "Active", "Done")
            },
        };

        // Act
        var projection = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Assert
        Assert.AreEqual(1, projection.BugsCreatedCount, "Bug created during sprint should be counted");
        Assert.AreEqual(1, projection.BugsClosedCount, "Bug closed during sprint should be counted");
    }

    [TestMethod]
    public void ComputeProgressionDelta_ReturnsZero_WhenNoFeatures()
    {
        var resolved = new List<ResolvedWorkItemEntity>();
        var workItems = new Dictionary<int, WorkItemEntity>();
        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        var delta = SprintTrendProjectionService.ComputeProgressionDelta(resolved, workItems, activity);

        Assert.AreEqual(0, delta);
    }

    [TestMethod]
    public void ComputeProgressionDelta_CalculatesEffortWeightedCompletion()
    {
        // Feature with 2 PBIs: one Done (effort 50), one Active (effort 100)
        // Expected: 50 / 150 * 100 = 33.33%
        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 100, state: "Active", parentId: 100),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new() { CreateActivity(201, DateTimeOffset.UtcNow) },
        };

        var delta = SprintTrendProjectionService.ComputeProgressionDelta(resolved, workItems, activity);

        Assert.AreEqual(33.33, delta, 0.01, "Progression should be 33.33% (50/150 * 100)");
    }

    #region ComputeFeatureProgress Tests

    [TestMethod]
    public void ComputeFeatureProgress_ReturnsEmpty_WhenNoFeatures()
    {
        var resolved = new List<ResolvedWorkItemEntity>();
        var workItems = new Dictionary<int, WorkItemEntity>();
        var productIds = new List<int> { 1 };

        var result = SprintTrendProjectionService.ComputeFeatureProgress(resolved, workItems, productIds);

        Assert.IsEmpty(result, "Should return empty when no features");
    }

    [TestMethod]
    public void ComputeFeatureProgress_CalculatesEffortWeightedProgress()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 5, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 10, state: "Active", parentId: 100),
            [203] = CreateWorkItem(203, WorkItemType.Pbi, "PBI 3", effort: 5, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 203, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = SprintTrendProjectionService.ComputeFeatureProgress(resolved, workItems, new List<int> { 1 });

        Assert.HasCount(1, result);
        var feature = result[0];
        Assert.AreEqual(100, feature.FeatureId);
        Assert.AreEqual("Feature A", feature.FeatureTitle);
        Assert.AreEqual(20, feature.TotalEffort, "Total effort should be 5+10+5=20");
        Assert.AreEqual(10, feature.DoneEffort, "Done effort should be 5+5=10");
        Assert.AreEqual(50, feature.ProgressPercent, "Progress should be 50% (10/20)");
        Assert.IsFalse(feature.IsDone);
    }

    [TestMethod]
    public void ComputeFeatureProgress_DoneFeature_Shows100Percent()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Done Feature", state: "Done"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 5, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 5, state: "Active", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = SprintTrendProjectionService.ComputeFeatureProgress(resolved, workItems, new List<int> { 1 });

        Assert.HasCount(1, result);
        Assert.AreEqual(100, result[0].ProgressPercent, "Done feature should show 100%");
        Assert.IsTrue(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeFeatureProgress_NonDoneFeature_CappedAt90Percent()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Active Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 5, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 5, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = SprintTrendProjectionService.ComputeFeatureProgress(resolved, workItems, new List<int> { 1 });

        Assert.HasCount(1, result);
        Assert.AreEqual(90, result[0].ProgressPercent, "Non-done feature with all PBIs done should be capped at 90%");
        Assert.IsFalse(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeFeatureProgress_IncludesEpicInfo()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [50] = CreateWorkItem(50, WorkItemType.Epic, "Epic X"),
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 50, WorkItemType = WorkItemType.Epic, ResolvedProductId = 1 },
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedEpicId = 50 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = SprintTrendProjectionService.ComputeFeatureProgress(resolved, workItems, new List<int> { 1 });

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].EpicId);
        Assert.AreEqual("Epic X", result[0].EpicTitle);
    }

    [TestMethod]
    public void ComputeFeatureProgress_SkipsFeaturesWithoutPbis()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Empty Feature"),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
        };

        var result = SprintTrendProjectionService.ComputeFeatureProgress(resolved, workItems, new List<int> { 1 });

        Assert.IsEmpty(result, "Features with no child PBIs should be skipped");
    }

    [TestMethod]
    public void ComputeFeatureProgress_MissingEffort_UsesSiblingAverage()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: null, state: "Active", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = SprintTrendProjectionService.ComputeFeatureProgress(resolved, workItems, new List<int> { 1 });

        Assert.HasCount(1, result);
        // PBI2 missing effort → sibling avg = 10, so total = 10 + 10 = 20, done = 10
        Assert.AreEqual(20, result[0].TotalEffort, "Total should use sibling average for missing effort");
        Assert.AreEqual(10, result[0].DoneEffort);
        Assert.AreEqual(50, result[0].ProgressPercent);
    }

    #endregion

    #region ComputeEpicProgress Tests

    [TestMethod]
    public void ComputeEpicProgress_AggregatesFromChildFeatures()
    {
        var featureProgress = new List<FeatureProgressDto>
        {
            new()
            {
                FeatureId = 200,
                FeatureTitle = "Feature A",
                EpicId = 100,
                EpicTitle = "Epic X",
                ProductId = 1,
                ProgressPercent = 50,
                TotalEffort = 20,
                DoneEffort = 10,
                IsDone = false
            },
            new()
            {
                FeatureId = 201,
                FeatureTitle = "Feature B",
                EpicId = 100,
                EpicTitle = "Epic X",
                ProductId = 1,
                ProgressPercent = 80,
                TotalEffort = 10,
                DoneEffort = 8,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic X", state: "Active"),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = SprintTrendProjectionService.ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(100, result[0].EpicId);
        Assert.AreEqual("Epic X", result[0].EpicTitle);
        Assert.AreEqual(30, result[0].TotalEffort, "Total effort should be 20+10=30");
        Assert.AreEqual(18, result[0].DoneEffort, "Done effort should be 10+8=18");
        Assert.AreEqual(60, result[0].ProgressPercent, "Progress should be ~60% (18/30)");
        Assert.AreEqual(2, result[0].FeatureCount);
        Assert.AreEqual(0, result[0].DoneFeatureCount);
        Assert.IsFalse(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeEpicProgress_DoneEpic_Shows100Percent()
    {
        var featureProgress = new List<FeatureProgressDto>
        {
            new()
            {
                FeatureId = 200,
                FeatureTitle = "Feature A",
                EpicId = 100,
                EpicTitle = "Done Epic",
                ProductId = 1,
                ProgressPercent = 50,
                TotalEffort = 10,
                DoneEffort = 5,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Done Epic", state: "Done"),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = SprintTrendProjectionService.ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(100, result[0].ProgressPercent, "Done epic should show 100%");
        Assert.IsTrue(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeEpicProgress_NonDoneEpic_CappedAt90Percent()
    {
        var featureProgress = new List<FeatureProgressDto>
        {
            new()
            {
                FeatureId = 200,
                FeatureTitle = "Feature A",
                EpicId = 100,
                EpicTitle = "Active Epic",
                ProductId = 1,
                ProgressPercent = 90,
                TotalEffort = 10,
                DoneEffort = 10,
                IsDone = true
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Active Epic", state: "Active"),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = SprintTrendProjectionService.ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(90, result[0].ProgressPercent, "Non-done epic should be capped at 90%");
        Assert.IsFalse(result[0].IsDone);
        Assert.AreEqual(1, result[0].DoneFeatureCount);
    }

    [TestMethod]
    public void ComputeEpicProgress_SkipsFeaturesWithoutEpic()
    {
        var featureProgress = new List<FeatureProgressDto>
        {
            new()
            {
                FeatureId = 200,
                FeatureTitle = "Standalone Feature",
                EpicId = null,
                EpicTitle = null,
                ProductId = 1,
                ProgressPercent = 50,
                TotalEffort = 10,
                DoneEffort = 5,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>();
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = SprintTrendProjectionService.ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.IsEmpty(result, "Features without epics should not produce epic progress");
    }

    #endregion

    #region Staleness Detection Tests

    [TestMethod]
    public void GetSprintTrendMetricsResponse_IsStale_WhenActivityAfterProjection()
    {
        var response = new GetSprintTrendMetricsResponse
        {
            Success = true,
            IsStale = true,
            ProjectionsAsOfUtc = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero)
        };

        Assert.IsTrue(response.IsStale);
        Assert.IsNotNull(response.ProjectionsAsOfUtc);
    }

    [TestMethod]
    public void GetSprintTrendMetricsResponse_NotStale_WhenNoActivity()
    {
        var response = new GetSprintTrendMetricsResponse
        {
            Success = true,
            IsStale = false,
            ProjectionsAsOfUtc = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero)
        };

        Assert.IsFalse(response.IsStale);
    }

    #endregion

    #region Batch Loading / Multi-Sprint Tests

    [TestMethod]
    public void ComputeProductSprintProjection_MultiSprint_ProducesCorrectResults()
    {
        // Test that separate sprint projections work independently even when 
        // activity events from different sprints are present
        var sprint1 = CreateSprint(1, "Sprint 1");
        var sprint2 = CreateSprintWithDates(2, "Sprint 2",
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc));

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 2 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 100, state: "Done", parentId: 100),
        };

        // Sprint 1 activity: PBI 201 transitions to Done in Sprint 1
        var sprint1Start = new DateTimeOffset(sprint1.StartDateUtc!.Value, TimeSpan.Zero);
        var sprint1End = new DateTimeOffset(sprint1.EndDateUtc!.Value, TimeSpan.Zero);
        var sprint1Activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new() { CreateStateChangeActivity(201, sprint1Start.AddDays(5), "Active", "Done") }
        };

        // Sprint 2 activity: PBI 202 transitions to Done in Sprint 2
        var sprint2Start = new DateTimeOffset(sprint2.StartDateUtc!.Value, TimeSpan.Zero);
        var sprint2End = new DateTimeOffset(sprint2.EndDateUtc!.Value, TimeSpan.Zero);
        var sprint2Activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [202] = new() { CreateStateChangeActivity(202, sprint2Start.AddDays(3), "Active", "Done") }
        };

        // Compute projection for Sprint 1
        var result1 = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint1, 1, resolved, workItems, sprint1Activity, sprint1Start, sprint1End);

        // Compute projection for Sprint 2
        var result2 = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint2, 1, resolved, workItems, sprint2Activity, sprint2Start, sprint2End);

        // Sprint 1 should have 1 completed PBI with 50 effort
        Assert.AreEqual(1, result1.CompletedPbiCount, "Sprint 1 should have 1 completed PBI");
        Assert.AreEqual(50, result1.CompletedPbiEffort, "Sprint 1 completed effort should be 50");

        // Sprint 2 should have 1 completed PBI with 100 effort
        Assert.AreEqual(1, result2.CompletedPbiCount, "Sprint 2 should have 1 completed PBI");
        Assert.AreEqual(100, result2.CompletedPbiEffort, "Sprint 2 completed effort should be 100");
    }

    #endregion

    #region Activity Bubbling and Planned Tests

    [TestMethod]
    public void ComputeProductSprintProjection_ActivityBubblesUpToParentFeature()
    {
        // When a Task under a PBI under a Feature has activity, the Feature should count as "worked"
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 301, WorkItemType = WorkItemType.Task, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Active", parentId: 100),
            [301] = CreateWorkItem(301, WorkItemType.Task, "Task 1", state: "Active", parentId: 201),
        };

        // Only the task has activity — should bubble up to PBI and Feature
        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [301] = new() { CreateStateChangeActivity(301, sprintStart.AddDays(3), "New", "Active") }
        };

        var result = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Feature and PBI should both count as "worked" due to child activity
        Assert.IsGreaterThanOrEqualTo(result.WorkedCount, 2, "WorkedCount should include Feature and PBI due to task child activity");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_PlannedCount_MatchesResolvedSprint()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            // 2 PBIs resolved to sprint 1, 1 PBI resolved to sprint 2, 1 bug resolved to sprint 1
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 203, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 2 },
            new() { WorkItemId = 204, WorkItemType = WorkItemType.Bug, ResolvedProductId = 1, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 30, state: "Active"),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 20, state: "Active"),
            [203] = CreateWorkItem(203, WorkItemType.Pbi, "PBI 3", effort: 10, state: "Active"),
            [204] = CreateWorkItem(204, WorkItemType.Bug, "Bug 1", state: "Active"),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        var result = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(2, result.PlannedCount, "PlannedCount should be 2 (only PBIs resolved to sprint 1)");
        Assert.AreEqual(50, result.PlannedEffort, "PlannedEffort should be 30+20=50");
        Assert.AreEqual(1, result.BugsPlannedCount, "BugsPlannedCount should be 1 (bug resolved to sprint 1)");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_NoActivity_ReturnsZeroWorked()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Active", parentId: 100),
        };

        // No activity events at all
        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        var result = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(0, result.WorkedCount, "WorkedCount should be 0 with no activity");
        Assert.AreEqual(0, result.CompletedPbiCount, "CompletedPbiCount should be 0");
        Assert.AreEqual(0, result.BugsCreatedCount, "BugsCreatedCount should be 0");
        Assert.AreEqual(0, result.BugsClosedCount, "BugsClosedCount should be 0");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_MetadataOnlyActivity_DoesNotCountAsWorked()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Active", parentId: 100),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateActivity(201, sprintStart.AddDays(2), "System.ChangedBy"),
                CreateActivity(201, sprintStart.AddDays(2), "System.ChangedDate")
            }
        };

        var result = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(0, result.WorkedCount, "Metadata-only changes should not count as worked activity");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_FunctionalActivity_CountsAsWorked()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Active", parentId: 100),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateActivity(201, sprintStart.AddDays(2), "System.Title")
            }
        };

        var result = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.IsGreaterThan(0, result.WorkedCount, "Functional changes should count as worked activity");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_MixedMetadataAndFunctionalActivity_CountsAsWorked()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Active", parentId: 100),
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateActivity(201, sprintStart.AddDays(2), "System.ChangedBy"),
                CreateActivity(201, sprintStart.AddDays(2), "System.ChangedDate"),
                CreateActivity(201, sprintStart.AddDays(2), "System.Title")
            }
        };

        var result = SprintTrendProjectionService.ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.IsGreaterThan(0, result.WorkedCount, "Mixed revisions should count as worked when functional fields changed");
    }

    #endregion

    // Helper methods

    private static SprintEntity CreateSprint(int id, string name)
    {
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc);
        return new SprintEntity
        {
            Id = id,
            TeamId = 1,
            Name = name,
            Path = $"\\Project\\{name}",
            StartUtc = new DateTimeOffset(startDate, TimeSpan.Zero),
            StartDateUtc = startDate,
            EndUtc = new DateTimeOffset(endDate, TimeSpan.Zero),
            EndDateUtc = endDate,
        };
    }

    private static SprintEntity CreateSprintWithDates(int id, string name, DateTime startDate, DateTime endDate)
    {
        return new SprintEntity
        {
            Id = id,
            TeamId = 1,
            Name = name,
            Path = $"\\Project\\{name}",
            StartUtc = new DateTimeOffset(startDate, TimeSpan.Zero),
            StartDateUtc = startDate,
            EndUtc = new DateTimeOffset(endDate, TimeSpan.Zero),
            EndDateUtc = endDate,
        };
    }

    private static WorkItemEntity CreateWorkItem(
        int tfsId, string type, string title,
        int? effort = null, string state = "New",
        int? parentId = null, DateTimeOffset? createdDate = null)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            Effort = effort,
            State = state,
            ParentTfsId = parentId,
            AreaPath = "\\Project",
            IterationPath = "\\Project\\Sprint 1",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            CreatedDate = createdDate,
        };
    }

    private static ActivityEventLedgerEntryEntity CreateStateChangeActivity(
        int workItemId, DateTimeOffset timestamp, string oldValue, string newValue)
    {
        return new ActivityEventLedgerEntryEntity
        {
            WorkItemId = workItemId,
            ProductOwnerId = 1,
            UpdateId = 1,
            FieldRefName = "System.State",
            EventTimestamp = timestamp,
            OldValue = oldValue,
            NewValue = newValue,
        };
    }

    private static ActivityEventLedgerEntryEntity CreateActivity(
        int workItemId, DateTimeOffset timestamp, string fieldRefName = "System.Title")
    {
        return new ActivityEventLedgerEntryEntity
        {
            WorkItemId = workItemId,
            ProductOwnerId = 1,
            UpdateId = 1,
            FieldRefName = fieldRefName,
            EventTimestamp = timestamp,
        };
    }
}
