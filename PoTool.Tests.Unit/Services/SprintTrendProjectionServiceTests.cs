using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SprintTrendProjectionServiceTests
{
    [TestMethod]
    public async Task ComputeProjectionsAsync_ReturnsEmpty_WhenNoSprintIds()
    {
        using var defaultServices = CreateDefaultServices();
        var service = CreateProjectionService(defaultServices);

        var projections = await service.ComputeProjectionsAsync(1, Array.Empty<int>());

        Assert.HasCount(0, projections);
    }

    [TestMethod]
    public async Task ComputeFeatureProgressAsync_NonDefaultEstimationMode_LogsWarning()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"SprintTrendProjection_EstimationGuard_{Guid.NewGuid()}")
            .Options;

        await using var context = new PoToolDbContext(options);
        context.Products.Add(new ProductEntity
        {
            Id = 1,
            ProductOwnerId = 7,
            Name = "Product A",
            EstimationMode = (int)PoTool.Shared.Settings.EstimationMode.Mixed,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        using var services = CreateDefaultServices();
        var logger = new Mock<ILogger<SprintTrendProjectionService>>();
        var scopeFactory = BuildScopeFactory(context, services);
        var service = new SprintTrendProjectionService(
            scopeFactory,
            logger.Object,
            stateClassificationService: null,
            services.GetRequiredService<ICanonicalStoryPointResolutionService>(),
            services.GetRequiredService<IHierarchyRollupService>(),
            services.GetRequiredService<IDeliveryProgressRollupService>(),
            services.GetRequiredService<ISprintCommitmentService>(),
            services.GetRequiredService<ISprintCompletionService>(),
            services.GetRequiredService<ISprintSpilloverService>(),
            services.GetRequiredService<ISprintDeliveryProjectionService>());

        var result = await service.ComputeFeatureProgressAsync(7, FeatureProgressMode.StoryPoints);

        Assert.HasCount(0, result);
        logger.Verify(
            instance => instance.Log(
                It.Is<LogLevel>(level => level == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state != null && state.ToString()!.Contains("Product 1 is configured with EstimationMode Mixed", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private static SprintMetricsProjectionEntity ComputeProductSprintProjection(
        SprintEntity sprint,
        int productId,
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem,
        DateTimeOffset sprintStart,
        DateTimeOffset sprintEnd,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null,
        IReadOnlyDictionary<int, DateTimeOffset>? firstDoneByWorkItem = null,
        IReadOnlySet<int>? committedWorkItemIds = null,
        string? nextSprintPath = null,
        IReadOnlyDictionary<int, WorkItemSnapshot>? workItemSnapshotsById = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? stateEventsByWorkItem = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? iterationEventsByWorkItem = null)
    {
        using var services = CreateDefaultServices();
        var service = CreateProjectionService(services);
        var effectiveWorkItemSnapshotsById = workItemSnapshotsById
            ?? workItemsByTfsId.Values.ToSnapshotDictionary();
        var effectiveIterationEventsByWorkItem = iterationEventsByWorkItem
            ?? activityByWorkItem.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<FieldChangeEvent>)pair.Value
                    .ToFieldChangeEvents()
                    .Where(change => string.Equals(change.FieldRefName, "System.IterationPath", StringComparison.OrdinalIgnoreCase))
                    .ToList());
        var effectiveCommittedWorkItemIds = committedWorkItemIds
            ?? services.GetRequiredService<ISprintCommitmentService>().BuildCommittedWorkItemIds(
                effectiveWorkItemSnapshotsById,
                effectiveIterationEventsByWorkItem,
                sprint.Path,
                services.GetRequiredService<ISprintCommitmentService>().GetCommitmentTimestamp(sprintStart));

        return service.ComputeProductSprintProjection(
            sprint,
            productId,
            resolvedItems,
            workItemsByTfsId,
            activityByWorkItem,
            sprintStart,
            sprintEnd,
            effectiveCommittedWorkItemIds,
            stateLookup,
            firstDoneByWorkItem,
            nextSprintPath,
            effectiveWorkItemSnapshotsById,
            stateEventsByWorkItem,
            effectiveIterationEventsByWorkItem);
    }

    private static double ComputeProgressionDelta(
        IReadOnlyList<ResolvedWorkItemEntity> productResolved,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        using var services = CreateDefaultServices();
        var service = CreateProjectionService(services);

        return service.ComputeProgressionDelta(
            productResolved,
            workItemsByTfsId,
            activityByWorkItem,
            stateLookup);
    }

    private static IReadOnlyList<FeatureProgressDto> ComputeFeatureProgress(
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyList<int> productIds,
        FeatureProgressMode progressMode,
        IReadOnlyCollection<int>? activeWorkItemIds = null,
        IReadOnlyCollection<int>? sprintCompletedPbiIds = null,
        IReadOnlyDictionary<int, int>? sprintEffortDeltaByWorkItem = null,
        IReadOnlyCollection<int>? sprintAssignedPbiIds = null,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        using var services = CreateDefaultServices();

        return SprintTrendProjectionService.ComputeFeatureProgress(
            resolvedItems,
            workItemsByTfsId,
            productIds,
            progressMode,
            services.GetRequiredService<IDeliveryProgressRollupService>(),
            services.GetRequiredService<ICanonicalStoryPointResolutionService>(),
            activeWorkItemIds,
            sprintCompletedPbiIds,
            sprintEffortDeltaByWorkItem,
            sprintAssignedPbiIds,
            stateLookup);
    }

    private static IReadOnlyList<EpicProgressDto> ComputeEpicProgress(
        IReadOnlyList<FeatureProgressDto> featureProgress,
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        using var services = CreateDefaultServices();

        return SprintTrendProjectionService.ComputeEpicProgress(
            featureProgress,
            resolvedItems,
            workItemsByTfsId,
            services.GetRequiredService<IDeliveryProgressRollupService>(),
            stateLookup);
    }

    private static ServiceProvider CreateDefaultServices()
    {
        var storyPointResolutionService = new CanonicalStoryPointResolutionService();
        var hierarchyRollupService = new HierarchyRollupService(storyPointResolutionService);
        var deliveryProgressRollupService = new DeliveryProgressRollupService(
            storyPointResolutionService,
            hierarchyRollupService);
        var sprintCompletionService = new SprintCompletionService();
        var sprintSpilloverService = new SprintSpilloverService();
        var sprintDeliveryProjectionService = new SprintDeliveryProjectionService(
            storyPointResolutionService,
            hierarchyRollupService,
            deliveryProgressRollupService,
            sprintCompletionService,
            sprintSpilloverService);

        return new ServiceCollection()
            .AddSingleton<ISprintCommitmentService, SprintCommitmentService>()
            .AddSingleton<ISprintCompletionService>(sprintCompletionService)
            .AddSingleton<ISprintSpilloverService>(sprintSpilloverService)
            .AddSingleton<ISprintScopeChangeService, SprintScopeChangeService>()
            .AddSingleton<ICanonicalStoryPointResolutionService>(storyPointResolutionService)
            .AddSingleton<IHierarchyRollupService>(hierarchyRollupService)
            .AddSingleton<IDeliveryProgressRollupService>(deliveryProgressRollupService)
            .AddSingleton<ISprintDeliveryProjectionService>(sprintDeliveryProjectionService)
            .BuildServiceProvider();
    }

    private static SprintTrendProjectionService CreateProjectionService(ServiceProvider services)
    {
        return new SprintTrendProjectionService(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SprintTrendProjectionService>.Instance,
            stateClassificationService: null,
            services.GetRequiredService<ICanonicalStoryPointResolutionService>(),
            services.GetRequiredService<IHierarchyRollupService>(),
            services.GetRequiredService<IDeliveryProgressRollupService>(),
            services.GetRequiredService<ISprintCommitmentService>(),
            services.GetRequiredService<ISprintCompletionService>(),
            services.GetRequiredService<ISprintSpilloverService>(),
            services.GetRequiredService<ISprintDeliveryProjectionService>());
    }

    private static IServiceScopeFactory BuildScopeFactory(PoToolDbContext context, ServiceProvider domainServices)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<PoToolDbContext>(context);
        services.AddSingleton(domainServices.GetRequiredService<ISprintCommitmentService>());
        services.AddSingleton(domainServices.GetRequiredService<ISprintCompletionService>());
        services.AddSingleton(domainServices.GetRequiredService<ISprintSpilloverService>());
        services.AddSingleton(domainServices.GetRequiredService<ICanonicalStoryPointResolutionService>());
        services.AddSingleton(domainServices.GetRequiredService<IHierarchyRollupService>());
        services.AddSingleton(domainServices.GetRequiredService<IDeliveryProgressRollupService>());
        services.AddSingleton(domainServices.GetRequiredService<ISprintDeliveryProjectionService>());
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
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
        var projection = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Assert
        Assert.AreEqual(1, projection.CompletedPbiCount, "One PBI transitioned to Done");
        Assert.AreEqual(50, projection.CompletedPbiEffort, "Completed PBI effort should be 50");
        Assert.IsGreaterThan(0.0, projection.ProgressionDelta, "Progression delta should be positive");
        Assert.AreEqual(0, projection.MissingEffortCount, "No missing effort");
        Assert.IsFalse(projection.IsApproximate, "No approximation needed");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_StoresStoryPointTotalsSeparatelyFromEffort()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Delivered PBI", effort: 40, state: "Done", parentId: 100, storyPoints: 8),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "Planned PBI", effort: 20, state: "Active", parentId: 100, storyPoints: 5)
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateStateChangeActivity(201, sprintStart.AddDays(1), "Active", "Done")
            }
        };

        var projection = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(60, projection.PlannedEffort, "Effort totals should continue to use effort hours.");
        Assert.AreEqual(13d, projection.PlannedStoryPoints, 0.001d, "Story-point totals should be stored independently from effort.");
        Assert.AreEqual(40, projection.CompletedPbiEffort, "Completed effort should remain effort-based.");
        Assert.AreEqual(8d, projection.CompletedPbiStoryPoints, 0.001d, "Delivered story points should reflect canonical story-point values.");
        Assert.AreEqual(0, projection.UnestimatedDeliveryCount, "Authoritatively estimated delivery should not count as unestimated.");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_PreservesDerivedStoryPointDiagnostics()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Estimated PBI", effort: 50, state: "Done", parentId: 100, storyPoints: 5),
            [202] = new WorkItemEntity
            {
                TfsId = 202,
                Type = WorkItemType.Pbi,
                Title = "Derived PBI",
                Effort = null,
                StoryPoints = null,
                BusinessValue = null,
                State = "Done",
                ParentTfsId = 100,
                AreaPath = "\\Project",
                IterationPath = "\\Project\\Sprint 1",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow
            }
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateStateChangeActivity(201, sprintStart.AddDays(1), "Active", "Done")
            },
            [202] = new()
            {
                CreateStateChangeActivity(202, sprintStart.AddDays(2), "Active", "Done")
            }
        };

        var projection = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(10d, projection.PlannedStoryPoints, 0.001d, "Planned story points should include derived estimates for aggregation.");
        Assert.AreEqual(1, projection.DerivedStoryPointCount, "Derived planned PBIs should be counted separately.");
        Assert.AreEqual(5d, projection.DerivedStoryPoints, 0.001d, "Derived story points should preserve their aggregate value.");
        Assert.AreEqual(0, projection.MissingStoryPointCount, "Derived estimates should not remain in the missing bucket.");
        Assert.AreEqual(5d, projection.CompletedPbiStoryPoints, 0.001d, "Velocity-aligned delivery should exclude derived estimates.");
        Assert.AreEqual(1, projection.UnestimatedDeliveryCount, "Derived deliveries should be surfaced as unestimated delivery.");
        Assert.IsTrue(projection.IsApproximate, "Derived estimates should keep the projection flagged as approximate.");
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
        var projection = ComputeProductSprintProjection(
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
        var projection = ComputeProductSprintProjection(
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
        var projection = ComputeProductSprintProjection(
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
        var projection = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Assert
        Assert.AreEqual(1, projection.BugsCreatedCount, "Bug created during sprint should be counted");
        Assert.AreEqual(1, projection.BugsClosedCount, "Bug closed during sprint should be counted");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_UsesCanonicalDoneMappingForResolvedStates()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100, ResolvedSprintId = 1 },
            new() { WorkItemId = 300, WorkItemType = WorkItemType.Bug, ResolvedProductId = 1, ResolvedSprintId = 1 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 13, state: "Resolved", parentId: 100),
            [300] = CreateWorkItem(300, WorkItemType.Bug, "Bug 1", state: "Resolved", createdDate: sprintStart.AddDays(1))
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new() { CreateStateChangeActivity(201, sprintStart.AddDays(2), "Active", "Resolved") },
            [300] = new() { CreateStateChangeActivity(300, sprintStart.AddDays(3), "Active", "Resolved") }
        };

        var projection = ComputeProductSprintProjection(
            sprint,
            1,
            resolved,
            workItems,
            activity,
            sprintStart,
            sprintEnd,
            BuildStateLookup(
                (WorkItemType.Pbi, "Resolved", StateClassification.Done),
                (WorkItemType.Bug, "Resolved", StateClassification.Done)));

        Assert.AreEqual(1, projection.CompletedPbiCount, "Resolved PBI should count as completed when mapped to canonical Done");
        Assert.AreEqual(13, projection.CompletedPbiEffort);
        Assert.AreEqual(1, projection.BugsClosedCount, "Resolved bug should count as closed when mapped to canonical Done");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_DoneReopenedDone_CountsOnlyFirstDoneDelivery()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 1 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 8, state: "Done")
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateStateChangeActivity(201, sprintStart.AddDays(2), "Active", "Done"),
                CreateStateChangeActivity(201, sprintStart.AddDays(4), "Done", "Active"),
                CreateStateChangeActivity(201, sprintStart.AddDays(6), "Active", "Done")
            }
        };

        var projection = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(1, projection.CompletedPbiCount, "Only the first Done transition should count as delivery");
        Assert.AreEqual(8, projection.CompletedPbiEffort);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_DoneBeforeSprint_ReopenedDuringSprint_DoesNotCountDelivery()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 1 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 8, state: "Done")
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateStateChangeActivity(201, sprintStart.AddDays(-1), "Active", "Done"),
                CreateStateChangeActivity(201, sprintStart.AddDays(3), "Done", "Active"),
                CreateStateChangeActivity(201, sprintStart.AddDays(5), "Active", "Done")
            }
        };

        var projection = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(0, projection.CompletedPbiCount, "A second Done transition must not create a new sprint delivery");
        Assert.AreEqual(0, projection.CompletedPbiEffort);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_FirstDoneInsideSprint_CountsCanonicalDelivery()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 1 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 13, state: "Resolved")
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateStateChangeActivity(201, sprintStart.AddDays(-2), "New", "Active"),
                CreateStateChangeActivity(201, sprintStart.AddDays(2), "Active", "Resolved")
            }
        };

        var projection = ComputeProductSprintProjection(
            sprint,
            1,
            resolved,
            workItems,
            activity,
            sprintStart,
            sprintEnd,
            BuildStateLookup((WorkItemType.Pbi, "Resolved", StateClassification.Done)));

        Assert.AreEqual(1, projection.CompletedPbiCount, "The first canonical Done transition inside the sprint should count as delivery");
        Assert.AreEqual(13, projection.CompletedPbiEffort);
    }

    [TestMethod]
    public void ComputeProgressionDelta_ReturnsZero_WhenNoFeatures()
    {
        var resolved = new List<ResolvedWorkItemEntity>();
        var workItems = new Dictionary<int, WorkItemEntity>();
        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        var delta = ComputeProgressionDelta(resolved, workItems, activity);

        Assert.AreEqual(0, delta);
    }

    [TestMethod]
    public void ComputeProgressionDelta_CalculatesEffortWeightedCompletion()
    {
        // Feature with 2 PBIs: one Done (effort 50), one Active (effort 100)
        // PBI 201 has a state-change-to-Done activity in this sprint
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
            [201] = new() { CreateStateChangeActivity(201, DateTimeOffset.UtcNow, "Active", "Done") },
        };

        var delta = ComputeProgressionDelta(resolved, workItems, activity);

        Assert.AreEqual(33.33, delta, 0.01, "Progression should be 33.33% (50/150 * 100)");
    }

    [TestMethod]
    public void ComputeProgressionDelta_NonStatusActivity_DoesNotCountForProgression()
    {
        // Feature with a Done PBI but only non-status field activity — should yield 0 delta
        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Done", parentId: 100),
        };

        // Only a title change — not a status transition to Done
        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new() { CreateActivity(201, DateTimeOffset.UtcNow, "System.Title") },
        };

        var delta = ComputeProgressionDelta(resolved, workItems, activity);

        Assert.AreEqual(0, delta, "Non-status activity should not contribute to progression delta");
    }

    #region ComputeFeatureProgress Tests

    [TestMethod]
    public void ComputeFeatureProgress_ReturnsEmpty_WhenNoFeatures()
    {
        var resolved = new List<ResolvedWorkItemEntity>();
        var workItems = new Dictionary<int, WorkItemEntity>();
        var productIds = new List<int> { 1 };

        var result = ComputeFeatureProgress(resolved, workItems, productIds, progressMode: FeatureProgressMode.StoryPoints);

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

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        var feature = result[0];
        Assert.AreEqual(100, feature.FeatureId);
        Assert.AreEqual("Feature A", feature.FeatureTitle);
        Assert.AreEqual(20, feature.TotalStoryPoints, "Total effort should be 5+10+5=20");
        Assert.AreEqual(10, feature.DoneStoryPoints, "Done effort should be 5+5=10");
        Assert.AreEqual(50, feature.ProgressPercent, "Progress should be 50% (10/20)");
        Assert.IsFalse(feature.IsDone);
    }

    [TestMethod]
    public void ComputeFeatureProgress_CanonicallyDoneFeature_UsesChildCompletionForProgress()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Done Feature", state: "Resolved"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 5, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 5, state: "Active", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].ProgressPercent, "Feature progress should come from child PBIs, not the parent feature state.");
        Assert.AreEqual(50d, result[0].CalculatedProgress!.Value, 0.001d);
        Assert.AreEqual(50d, result[0].EffectiveProgress!.Value, 0.001d);
        Assert.IsTrue(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeFeatureProgress_UsesCanonicalDoneMappingForChildCompletionOnly()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Resolved Feature", state: "Resolved"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Resolved PBI", effort: 5, state: "Resolved", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "Active PBI", effort: 5, state: "Active", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = ComputeFeatureProgress(
            resolved,
            workItems,
            new List<int> { 1 },
            stateLookup: BuildStateLookup(
                (WorkItemType.Feature, "Resolved", StateClassification.Done),
                (WorkItemType.Pbi, "Resolved", StateClassification.Done)), progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].ProgressPercent, "Resolved feature state should not override child-completion-based progress.");
        Assert.AreEqual(5, result[0].DoneStoryPoints, "Resolved PBI should contribute to done effort");
        Assert.IsTrue(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeFeatureProgress_IgnoresExplicitModeForProgressCalculation()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Small done PBI", effort: 1, state: "Done", parentId: 100, storyPoints: 1),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "Large active PBI", effort: 9, state: "Active", parentId: 100, storyPoints: 9),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var storyPointModeResult = ComputeFeatureProgress(
            resolved,
            workItems,
            new List<int> { 1 },
            progressMode: FeatureProgressMode.StoryPoints);
        var countModeResult = ComputeFeatureProgress(
            resolved,
            workItems,
            new List<int> { 1 },
            progressMode: FeatureProgressMode.Count);

        Assert.HasCount(1, storyPointModeResult);
        Assert.HasCount(1, countModeResult);
        Assert.AreEqual(10, storyPointModeResult[0].ProgressPercent);
        Assert.AreEqual(10, countModeResult[0].ProgressPercent);
        Assert.AreEqual(10d, storyPointModeResult[0].CalculatedProgress!.Value, 0.001d);
        Assert.AreEqual(10d, countModeResult[0].CalculatedProgress!.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeFeatureProgress_NonDoneFeature_Reaches100PercentWhenAllPbisAreDone()
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

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(100, result[0].ProgressPercent, "Feature progress should not be capped below 100 when all PBIs are done.");
        Assert.AreEqual(100d, result[0].CalculatedProgress!.Value, 0.001d);
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

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].EpicId);
        Assert.AreEqual("Epic X", result[0].EpicTitle);
    }

    [TestMethod]
    public void ComputeFeatureProgress_IncludesFeaturesWithoutPbisWithZeroCalculatedProgress()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Empty Feature"),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
        };

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(0d, result[0].CalculatedProgress!.Value, 0.001d);
        Assert.AreEqual(0d, result[0].EffectiveProgress!.Value, 0.001d);
        Assert.AreEqual(0, result[0].ProgressPercent);
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithActivityFilter_ExcludesInactiveFeatures()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Active Feature", state: "Active"),
            [101] = CreateWorkItem(101, WorkItemType.Feature, "Inactive Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 10, state: "Active", parentId: 101),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 101, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 101 },
        };

        // Only PBI 201 had activity — Feature 101 / PBI 202 had none
        var activeWorkItemIds = new HashSet<int> { 201 };

        var result = ComputeFeatureProgress(
            resolved,
            workItems,
            new List<int> { 1 },
            progressMode: FeatureProgressMode.StoryPoints,
            activeWorkItemIds: activeWorkItemIds);

        Assert.HasCount(1, result, "Only the feature with child PBI activity should be returned");
        Assert.AreEqual(100, result[0].FeatureId);
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithActivityFilter_IncludesFeatureWithDirectActivity()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        // Feature itself had direct activity (e.g. title change), but no PBI activity
        var activeWorkItemIds = new HashSet<int> { 100 };

        var result = ComputeFeatureProgress(
            resolved,
            workItems,
            new List<int> { 1 },
            progressMode: FeatureProgressMode.StoryPoints,
            activeWorkItemIds: activeWorkItemIds);

        Assert.HasCount(1, result, "Feature with direct activity should be included even without PBI activity");
        Assert.AreEqual(100, result[0].FeatureId);
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithActivityFilter_IncludesFeatureWhenOnlyTaskDescendantIsActive()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Active", parentId: 100),
            [301] = CreateWorkItem(301, WorkItemType.Task, "Task 1", state: "Active", parentId: 201),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 301, WorkItemType = WorkItemType.Task, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var activeWorkItemIds = new HashSet<int> { 301 };

        var result = ComputeFeatureProgress(
            resolved,
            workItems,
            new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result, "Feature should remain visible when only a task descendant changed during the sprint.");
        Assert.AreEqual(100, result[0].FeatureId);
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithNullActivityFilter_IncludesAllFeaturesWithPbis()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [101] = CreateWorkItem(101, WorkItemType.Feature, "Feature B", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 10, state: "Active", parentId: 101),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 101, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 101 },
        };

        // No activity filter — both features should be returned
        var result = ComputeFeatureProgress(
            resolved,
            workItems,
            new List<int> { 1 },
            progressMode: FeatureProgressMode.StoryPoints,
            activeWorkItemIds: null);

        Assert.HasCount(2, result, "Without activity filter, all features with PBIs should be returned");
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithSprintAssignedPbiIds_IncludesFeatureWithNoActivityButSprintPbis()
    {
        // Epic visibility rule: a feature (and its epic) must be shown when any child PBI is
        // assigned to the sprint, even when Delivered=0 and there are no activity events.
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Active Feature", state: "Active"),
            [101] = CreateWorkItem(101, WorkItemType.Feature, "Sprint-Only Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1 (active)", effort: 10, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2 (sprint, no activity)", effort: 8, state: "Active", parentId: 101),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 101, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 101 },
        };

        // Only PBI 201 had activity events; PBI 202 is in the sprint but no events
        var activeWorkItemIds = new HashSet<int> { 201 };
        // PBI 202 is assigned to the sprint
        var sprintAssignedPbiIds = new HashSet<int> { 202 };

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds,
            sprintAssignedPbiIds: sprintAssignedPbiIds, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(2, result, "Both features should be returned: one with activity, one with sprint-assigned PBI");
        CollectionAssert.Contains(result.Select(f => f.FeatureId).ToList(), 100, "Feature 100 (activity) must be included");
        CollectionAssert.Contains(result.Select(f => f.FeatureId).ToList(), 101, "Feature 101 (sprint PBI, no activity) must be included");
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithActivityFilterOnly_ExcludesFeatureWhosePbisAreNotInActivityOrSprint()
    {
        // A feature with PBIs that have neither activity events nor sprint assignment must be excluded
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Active Feature", state: "Active"),
            [101] = CreateWorkItem(101, WorkItemType.Feature, "Excluded Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1 (active)", effort: 10, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2 (no sprint, no activity)", effort: 8, state: "Active", parentId: 101),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 101, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 101 },
        };

        var activeWorkItemIds = new HashSet<int> { 201 };
        // PBI 202 is NOT in the sprint
        var sprintAssignedPbiIds = new HashSet<int>(); // empty

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds,
            sprintAssignedPbiIds: sprintAssignedPbiIds, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result, "Only the feature with activity should be returned");
        Assert.AreEqual(100, result[0].FeatureId);
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithNullSprintAssignedPbiIds_FallsBackToActivityFilterOnly()
    {
        // When sprintAssignedPbiIds is null, behaviour is unchanged from before
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Active Feature", state: "Active"),
            [101] = CreateWorkItem(101, WorkItemType.Feature, "Inactive Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 8, state: "Active", parentId: 101),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 101, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 101 },
        };

        var activeWorkItemIds = new HashSet<int> { 201 };

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds,
            sprintAssignedPbiIds: null, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result, "Without sprintAssignedPbiIds, only activity-based filter applies");
        Assert.AreEqual(100, result[0].FeatureId);
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

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        // Story-point scope still uses sibling averaging, but feature progress now treats null effort as zero.
        Assert.AreEqual(20, result[0].TotalStoryPoints, "Total should use sibling average for missing story-point scope");
        Assert.AreEqual(10, result[0].DoneStoryPoints);
        Assert.AreEqual(100, result[0].ProgressPercent);
    }

    [TestMethod]
    public void ComputeFeatureProgress_UsesFeatureFallbackOnlyWhenChildPbisLackEstimates()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Fallback Feature", state: "Resolved", businessValue: 8),
            [101] = CreateWorkItem(101, WorkItemType.Feature, "Child Estimate Feature", state: "Active", businessValue: 13),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Missing PBI", effort: null, state: "Done", parentId: 100, storyPoints: null, businessValue: null),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "Estimated PBI", effort: 5, state: "Active", parentId: 101),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 101, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 101 },
        };

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(2, result);
        Assert.AreEqual(8d, result.Single(f => f.FeatureId == 100).TotalStoryPoints);
        Assert.AreEqual(8d, result.Single(f => f.FeatureId == 100).DoneStoryPoints);
        Assert.AreEqual(5d, result.Single(f => f.FeatureId == 101).TotalStoryPoints);
        Assert.AreEqual(0d, result.Single(f => f.FeatureId == 101).DoneStoryPoints);
    }

    [TestMethod]
    public void ComputeFeatureProgress_UsesFractionalDerivedStoryPointsWithoutRounding()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 3, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 4, state: "Active", parentId: 100),
            [203] = CreateWorkItem(203, WorkItemType.Pbi, "PBI 3", effort: null, state: "Active", parentId: 100, storyPoints: null, businessValue: null),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 203, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(10.5d, result[0].TotalStoryPoints, 0.001d);
        Assert.AreEqual(3d, result[0].DoneStoryPoints, 0.001d);
        Assert.AreEqual(43, result[0].ProgressPercent);
    }

    [TestMethod]
    public void ComputeFeatureProgress_ExcludesBugAndTaskStoryPoints()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 5, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Bug, "Bug 1", effort: 13, state: "Done", parentId: 100),
            [203] = CreateWorkItem(203, WorkItemType.Task, "Task 1", effort: 8, state: "Done", parentId: 201),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Bug, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 203, WorkItemType = WorkItemType.Task, ResolvedProductId = 1 },
        };

        var result = ComputeFeatureProgress(resolved, workItems, new List<int> { 1 }, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(5d, result[0].TotalStoryPoints);
        Assert.AreEqual(5d, result[0].DoneStoryPoints);
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
                EffectiveProgress = 50,
                Weight = 20,
                TotalStoryPoints = 20,
                DoneStoryPoints = 10,
                ForecastConsumedEffort = 20,
                ForecastRemainingEffort = 80,
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
                EffectiveProgress = 80,
                Weight = 10,
                TotalStoryPoints = 10,
                DoneStoryPoints = 8,
                ForecastConsumedEffort = 30,
                ForecastRemainingEffort = 70,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic X", state: "Active"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature A", parentId: 100, state: "Active", effort: 20),
            [201] = CreateWorkItem(201, WorkItemType.Feature, "Feature B", parentId: 100, state: "Active", effort: 10),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(100, result[0].EpicId);
        Assert.AreEqual("Epic X", result[0].EpicTitle);
        Assert.AreEqual(30, result[0].TotalStoryPoints, "Total effort should be 20+10=30");
        Assert.AreEqual(18, result[0].DoneStoryPoints, "Done effort should be 10+8=18");
        Assert.AreEqual(60, result[0].ProgressPercent, "Progress should be the weighted average rounded from child feature progress.");
        Assert.AreEqual(60d, result[0].AggregatedProgress!.Value, 0.001d);
        Assert.AreEqual(50d, result[0].ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(150d, result[0].ForecastRemainingEffort!.Value, 0.001d);
        Assert.AreEqual(2, result[0].FeatureCount);
        Assert.AreEqual(0, result[0].DoneFeatureCount);
        Assert.IsFalse(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeEpicProgress_DoneEpic_PreservesWeightedAverageProgress()
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
                EffectiveProgress = 50,
                Weight = 10,
                TotalStoryPoints = 10,
                DoneStoryPoints = 5,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Done Epic", state: "Resolved"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature A", parentId: 100, state: "Active", effort: 10),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].ProgressPercent, "Epic state should not override weighted feature progress.");
        Assert.AreEqual(50d, result[0].AggregatedProgress!.Value, 0.001d);
        Assert.IsTrue(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeEpicProgress_UsesCanonicalDoneMappingForStateWithoutOverridingProgress()
    {
        var featureProgress = new List<FeatureProgressDto>
        {
            new()
            {
                FeatureId = 200,
                FeatureTitle = "Feature A",
                EpicId = 100,
                EpicTitle = "Resolved Epic",
                ProductId = 1,
                ProgressPercent = 50,
                EffectiveProgress = 50,
                Weight = 10,
                TotalStoryPoints = 10,
                DoneStoryPoints = 5,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Resolved Epic", state: "Resolved"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature A", parentId: 100, state: "Active", effort: 10),
        };

        var result = ComputeEpicProgress(
            featureProgress,
            new List<ResolvedWorkItemEntity>(),
            workItems,
            BuildStateLookup((WorkItemType.Epic, "Resolved", StateClassification.Done)));

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].ProgressPercent, "Resolved epic should still use weighted child progress when canonically mapped done.");
        Assert.AreEqual(50d, result[0].AggregatedProgress!.Value, 0.001d);
        Assert.IsTrue(result[0].IsDone);
    }

    [TestMethod]
    public void ComputeEpicProgress_ExcludedFeaturesDoNotAffectProgress()
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
                ProgressPercent = 50,
                EffectiveProgress = 50,
                Weight = 2,
                TotalStoryPoints = 10,
                DoneStoryPoints = 5,
                IsDone = false
            },
            new()
            {
                FeatureId = 201,
                FeatureTitle = "Excluded Feature",
                EpicId = 100,
                EpicTitle = "Active Epic",
                ProductId = 1,
                ProgressPercent = 100,
                EffectiveProgress = 100,
                Weight = 0,
                IsExcluded = true,
                TotalStoryPoints = 0,
                DoneStoryPoints = 0,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Active Epic", state: "Active"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature A", parentId: 100, state: "Active", effort: 2),
            [201] = CreateWorkItem(201, WorkItemType.Feature, "Excluded Feature", parentId: 100, state: "Active", effort: 0),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].ProgressPercent, "Excluded features should not contribute to weighted epic progress.");
        Assert.AreEqual(50d, result[0].AggregatedProgress!.Value, 0.001d);
        Assert.IsFalse(result[0].IsDone);
        Assert.AreEqual(1, result[0].ExcludedFeaturesCount);
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
                TotalStoryPoints = 10,
                DoneStoryPoints = 5,
                IsDone = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>();
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = ComputeEpicProgress(featureProgress, resolvedItems, workItems);

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
        var result1 = ComputeProductSprintProjection(
            sprint1, 1, resolved, workItems, sprint1Activity, sprint1Start, sprint1End);

        // Compute projection for Sprint 2
        var result2 = ComputeProductSprintProjection(
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

        var result = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        // Task activity should bubble all the way through Task → PBI → Feature
        Assert.AreEqual(3, result.WorkedCount, "WorkedCount should include the task plus its PBI and Feature ancestors.");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_PlannedCount_UsesCommittedSprintMembership()
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
        workItems[203].IterationPath = "\\Project\\Sprint 2";

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [203] = new()
            {
                CreateIterationPathActivity(203, sprintStart.AddHours(12), sprint.Path, "\\Project\\Sprint 2")
            }
        };

        var result = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(2, result.PlannedCount, "PlannedCount should use committed sprint membership rather than ResolvedSprintId fallback.");
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

        var result = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(0, result.WorkedCount, "WorkedCount should be 0 with no activity");
        Assert.AreEqual(0, result.CompletedPbiCount, "CompletedPbiCount should be 0");
        Assert.AreEqual(0, result.BugsCreatedCount, "BugsCreatedCount should be 0");
        Assert.AreEqual(0, result.BugsClosedCount, "BugsClosedCount should be 0");
    }

    [TestMethod]
    public void ComputeProductSprintProjection_UsesCommittedScope_WhenItemMovedAwayAfterCommitment()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 2 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Moved PBI", effort: 13, state: "Active")
        };
        workItems[201].IterationPath = "\\Project\\Backlog";

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        var result = ComputeProductSprintProjection(
            sprint,
            1,
            resolved,
            workItems,
            activity,
            sprintStart,
            sprintEnd,
            committedWorkItemIds: new HashSet<int> { 201 });

        Assert.AreEqual(1, result.PlannedCount, "Committed items should remain planned even when current sprint membership changed later.");
        Assert.AreEqual(13, result.PlannedEffort);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_ExcludesItemsAddedAfterCommitment_FromPlannedScope()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = 1 }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Late Add", effort: 8, state: "Active")
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        var result = ComputeProductSprintProjection(
            sprint,
            1,
            resolved,
            workItems,
            activity,
            sprintStart,
            sprintEnd,
            committedWorkItemIds: new HashSet<int>());

        Assert.AreEqual(0, result.PlannedCount, "Items that were not on the sprint at commitment should not count as planned scope.");
        Assert.AreEqual(0, result.PlannedEffort);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_CountsCommittedPbiMovedDirectlyToNextSprint_AsSpillover()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var nextSprint = CreateSprintWithDates(2, "Sprint 2", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc));
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = nextSprint.Id }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Spillover PBI", effort: 8, state: "Active")
        };
        workItems[201].IterationPath = nextSprint.Path;

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateIterationPathActivity(201, sprintEnd.AddHours(1), sprint.Path, nextSprint.Path)
            }
        };

        var result = ComputeProductSprintProjection(
            sprint,
            1,
            resolved,
            workItems,
            activity,
            sprintStart,
            sprintEnd,
            committedWorkItemIds: new HashSet<int> { 201 },
            nextSprintPath: nextSprint.Path);

        Assert.AreEqual(1, result.SpilloverCount);
        Assert.AreEqual(8, result.SpilloverEffort);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_DoesNotCountBacklogRoundTripToNextSprint_AsSpillover()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var nextSprint = CreateSprintWithDates(2, "Sprint 2", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc));
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = nextSprint.Id }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Round Trip PBI", effort: 5, state: "Active")
        };
        workItems[201].IterationPath = nextSprint.Path;

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [201] = new()
            {
                CreateIterationPathActivity(201, sprintEnd.AddHours(1), sprint.Path, "\\Project\\Backlog"),
                CreateIterationPathActivity(201, sprintEnd.AddDays(2), "\\Project\\Backlog", nextSprint.Path)
            }
        };

        var result = ComputeProductSprintProjection(
            sprint,
            1,
            resolved,
            workItems,
            activity,
            sprintStart,
            sprintEnd,
            committedWorkItemIds: new HashSet<int> { 201 },
            nextSprintPath: nextSprint.Path);

        Assert.AreEqual(0, result.SpilloverCount);
        Assert.AreEqual(0, result.SpilloverEffort);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_DoesNotCountUnfinishedCommittedPbiStillOnSprintPath_AsSpillover()
    {
        var sprint = CreateSprint(1, "Sprint 1");
        var nextSprint = CreateSprintWithDates(2, "Sprint 2", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc));
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedSprintId = sprint.Id }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "Still In Sprint", effort: 3, state: "Active")
        };

        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>();

        var result = ComputeProductSprintProjection(
            sprint,
            1,
            resolved,
            workItems,
            activity,
            sprintStart,
            sprintEnd,
            committedWorkItemIds: new HashSet<int> { 201 },
            nextSprintPath: nextSprint.Path);

        Assert.AreEqual(0, result.SpilloverCount);
        Assert.AreEqual(0, result.SpilloverEffort);
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

        var result = ComputeProductSprintProjection(
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

        var result = ComputeProductSprintProjection(
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

        var result = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.IsGreaterThan(0, result.WorkedCount, "Mixed revisions should count as worked when functional fields changed");
    }

    #endregion

    #region Sprint-Scoped Feature/Epic Metrics Tests

    [TestMethod]
    public void ComputeFeatureProgress_WithSprintCompletedPbiIds_ComputesSprintMetrics()
    {
        // Feature with 2 PBIs; PBI 201 (effort=50) closed in sprint, PBI 202 (effort=100) not closed
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 100, state: "Active", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var sprintCompletedPbiIds = new HashSet<int> { 201 };

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: null, sprintCompletedPbiIds: sprintCompletedPbiIds, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(50, result[0].SprintCompletedEffort, "Sprint scored effort should equal effort of PBI closed in sprint");
        // SprintProgressionDelta = 50 / 150 * 100 = 33.33% (TotalStoryPoints = 50 done + 100 active = 150)
        Assert.IsGreaterThan(0.0, result[0].SprintProgressionDelta, "Sprint progression delta should be positive");
        Assert.AreEqual(33.33, result[0].SprintProgressionDelta, 0.01, "Sprint progression delta should be 33.33%");
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithNoSprintCompletedPbiIds_SprintMetricsAreZero()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 50, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: null, sprintCompletedPbiIds: null, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(0, result[0].SprintCompletedEffort, "Sprint scored effort should be 0 when no sprint filter provided");
        Assert.AreEqual(0.0, result[0].SprintProgressionDelta, "Sprint progression delta should be 0 when no sprint filter provided");
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithSprintCompletedPbiIds_EmptySet_SprintMetricsAreZero()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 60, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        // Sprint filter provided but no PBIs closed
        var sprintCompletedPbiIds = new HashSet<int>();

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: null, sprintCompletedPbiIds: sprintCompletedPbiIds, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(0, result[0].SprintCompletedEffort, "Sprint scored effort should be 0 when no PBIs closed in sprint");
        Assert.AreEqual(0.0, result[0].SprintProgressionDelta, "Sprint progression delta should be 0 when no PBIs closed in sprint");
    }

    [TestMethod]
    public void ComputeEpicProgress_AggregatesSprintMetricsFromFeatures()
    {
        // Two features under same epic; each with sprint-specific data
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
                EffectiveProgress = 50,
                Weight = 40,
                TotalStoryPoints = 40,
                DoneStoryPoints = 20,
                IsDone = false,
                SprintCompletedEffort = 10,
                SprintProgressionDelta = 25.0
            },
            new()
            {
                FeatureId = 201,
                FeatureTitle = "Feature B",
                EpicId = 100,
                EpicTitle = "Epic X",
                ProductId = 1,
                ProgressPercent = 80,
                EffectiveProgress = 80,
                Weight = 60,
                TotalStoryPoints = 60,
                DoneStoryPoints = 48,
                IsDone = false,
                SprintCompletedEffort = 20,
                SprintProgressionDelta = 33.33
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic X", state: "Active"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature A", parentId: 100, state: "Active", effort: 0),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(30, result[0].SprintCompletedEffort, "Epic sprint scored effort should aggregate from child features (10+20=30)");
        // SprintProgressionDelta = 30 / 100 * 100 = 30% (TotalStoryPoints = 40 + 60 = 100 from both features)
        Assert.AreEqual(30.0, result[0].SprintProgressionDelta, 0.01, "Epic sprint delta should be 30%");
    }

    [TestMethod]
    public void ComputeEpicProgress_ZeroTotalStoryPoints_SprintDeltaIsZero()
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
                ProgressPercent = 0,
                Weight = 0,
                IsExcluded = true,
                TotalStoryPoints = 0,
                DoneStoryPoints = 0,
                IsDone = false,
                SprintCompletedEffort = 0,
                SprintProgressionDelta = 0.0
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic X", state: "Active"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature A", parentId: 100, state: "Active", effort: 0),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(0.0, result[0].SprintProgressionDelta, "Sprint delta should be 0 when total effort is 0");
        Assert.AreEqual(0d, result[0].AggregatedProgress!.Value, 0.001d, "Weighted epic progress should be 0 when all features have zero weight.");
        Assert.AreEqual(0, result[0].ProgressPercent, "Deterministic epic progress should fall back to zero when no feature weight is available.");
        Assert.AreEqual(1, result[0].ExcludedFeaturesCount);
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithSprintCompletedPbiIds_ComputesSprintPbiCount()
    {
        // Feature with 3 PBIs; 2 closed in sprint, 1 not
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Active"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
            [202] = CreateWorkItem(202, WorkItemType.Pbi, "PBI 2", effort: 20, state: "Done", parentId: 100),
            [203] = CreateWorkItem(203, WorkItemType.Pbi, "PBI 3", effort: 30, state: "Active", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 202, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
            new() { WorkItemId = 203, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var sprintCompletedPbiIds = new HashSet<int> { 201, 202 };

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: null, sprintCompletedPbiIds: sprintCompletedPbiIds, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(2, result[0].SprintCompletedPbiCount, "SprintCompletedPbiCount should count only PBIs closed in sprint");
        Assert.IsFalse(result[0].SprintCompletedInSprint, "Feature itself was not closed in sprint");
    }

    [TestMethod]
    public void ComputeFeatureProgress_WhenFeatureClosedInSprint_SetsSprintCompletedInSprint()
    {
        // Feature itself (id=100) is in the closedInSprint set
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Done"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        // The closedInSprint set includes both the feature and its PBI
        var sprintCompletedPbiIds = new HashSet<int> { 100, 201 };

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: null, sprintCompletedPbiIds: sprintCompletedPbiIds, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.IsTrue(result[0].SprintCompletedInSprint, "Feature closed in sprint should be marked SprintCompletedInSprint=true");
        Assert.AreEqual(1, result[0].SprintCompletedPbiCount, "SprintCompletedPbiCount should count child PBI 201");
    }

    [TestMethod]
    public void ComputeFeatureProgress_WithNullSprintFilter_SprintCompletedCountsAreZero()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Feature, "Feature A", state: "Done"),
            [201] = CreateWorkItem(201, WorkItemType.Pbi, "PBI 1", effort: 10, state: "Done", parentId: 100),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 100, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1 },
            new() { WorkItemId = 201, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 100 },
        };

        var result = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: null, sprintCompletedPbiIds: null, progressMode: FeatureProgressMode.StoryPoints);

        Assert.HasCount(1, result);
        Assert.AreEqual(0, result[0].SprintCompletedPbiCount, "SprintCompletedPbiCount should be 0 when no sprint filter");
        Assert.IsFalse(result[0].SprintCompletedInSprint, "SprintCompletedInSprint should be false when no sprint filter");
    }

    [TestMethod]
    public void ComputeEpicProgress_AggregatesSprintCompletedPbiCountAndFeatureCount()
    {
        // Two features: Feature A completed in sprint with 2 PBIs done, Feature B not completed but 1 PBI done
        var featureProgress = new List<FeatureProgressDto>
        {
            new()
            {
                FeatureId = 200,
                FeatureTitle = "Feature A",
                EpicId = 100,
                EpicTitle = "Epic X",
                ProductId = 1,
                ProgressPercent = 100,
                EffectiveProgress = 100,
                Weight = 30,
                TotalStoryPoints = 30,
                DoneStoryPoints = 30,
                IsDone = true,
                SprintCompletedEffort = 30,
                SprintProgressionDelta = 100.0,
                SprintCompletedPbiCount = 2,
                SprintCompletedInSprint = true
            },
            new()
            {
                FeatureId = 201,
                FeatureTitle = "Feature B",
                EpicId = 100,
                EpicTitle = "Epic X",
                ProductId = 1,
                ProgressPercent = 50,
                EffectiveProgress = 50,
                Weight = 40,
                TotalStoryPoints = 40,
                DoneStoryPoints = 20,
                IsDone = false,
                SprintCompletedEffort = 20,
                SprintProgressionDelta = 50.0,
                SprintCompletedPbiCount = 1,
                SprintCompletedInSprint = false
            }
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic X", state: "Active"),
        };
        var resolvedItems = new List<ResolvedWorkItemEntity>();

        var result = ComputeEpicProgress(featureProgress, resolvedItems, workItems);

        Assert.HasCount(1, result);
        Assert.AreEqual(3, result[0].SprintCompletedPbiCount, "Epic SprintCompletedPbiCount should sum child feature counts (2+1=3)");
        Assert.AreEqual(1, result[0].SprintCompletedFeatureCount, "Epic SprintCompletedFeatureCount should count features completed in sprint");
    }

    #endregion

    #region Monitored Activity — Epic Visibility Acceptance Tests
    // These tests document the acceptance criteria for the "Fix Epic Visibility Using Monitored
    // Activity" issue: an epic must be visible when any descendant has a non-noise field change
    // during the sprint, even when Delivered pts = 0 / ΔEffort = 0 / PBIs completed = 0.

    [TestMethod]
    public void EpicVisibility_AcceptanceCriteria1_EpicAppearsWhenDescendantHasNonNoiseActivity_EvenIfDeliveredIsZero()
    {
        // Arrange: Epic E → Feature F → PBI P
        // PBI P has a System.Title change during the sprint (non-noise) but is NOT Done
        // → Delivered pts = 0, ΔEffort = 0, PBIs completed = 0
        // Expected: Epic E MUST appear in ComputeEpicProgress output.
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [10] = CreateWorkItem(10, WorkItemType.Epic, "Epic E", state: "Active"),
            [20] = CreateWorkItem(20, WorkItemType.Feature, "Feature F", state: "Active"),
            [30] = CreateWorkItem(30, WorkItemType.Pbi, "PBI P", effort: 50, state: "Active", parentId: 20),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 10, WorkItemType = WorkItemType.Epic, ResolvedProductId = 1 },
            new() { WorkItemId = 20, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedEpicId = 10 },
            new() { WorkItemId = 30, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 20 },
        };

        // PBI P had a non-noise activity event (System.Title change) — no state transition to Done
        var activeWorkItemIds = new HashSet<int> { 30 };

        var featureProgress = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds,
            sprintCompletedPbiIds: new HashSet<int>(), progressMode: FeatureProgressMode.StoryPoints);

        var epicProgress = ComputeEpicProgress(
            featureProgress, resolved, workItems);

        // Verify feature is included (non-noise activity gate passes)
        Assert.HasCount(1, featureProgress, "Feature F must be included because PBI P had non-noise activity");

        // Verify epic is in the result list (no delivery gate applied at this layer)
        Assert.HasCount(1, epicProgress, "Epic E must appear in epic progress when descendant PBI has non-noise activity");
        Assert.AreEqual(10, epicProgress[0].EpicId);

        // Verify all delivery metrics are zero — epic is visible due to activity, not delivery
        Assert.AreEqual(0, epicProgress[0].SprintCompletedEffort, "Delivered pts must be 0");
        Assert.AreEqual(0, epicProgress[0].SprintEffortDelta, "Δ Effort must be 0");
        Assert.AreEqual(0, epicProgress[0].SprintCompletedPbiCount, "PBIs completed must be 0");
    }

    [TestMethod]
    [DataRow("System.Title")]
    [DataRow("Microsoft.VSTS.Scheduling.Effort")]
    [DataRow("System.State")]
    [DataRow("System.IterationPath")]
    [DataRow("System.Reason")]
    public void EpicVisibility_AcceptanceCriteria1_MonitoredFieldChanges_EpicAppearsEvenWithZeroDelivery(string fieldRefName)
    {
        // Each of these fields is non-noise; a revision changing any one of them
        // must cause Epic E to appear even when Delivered pts = 0.
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [10] = CreateWorkItem(10, WorkItemType.Epic, "Epic E", state: "Active"),
            [20] = CreateWorkItem(20, WorkItemType.Feature, "Feature F", state: "Active"),
            [30] = CreateWorkItem(30, WorkItemType.Pbi, "PBI P", effort: 40, state: "Active", parentId: 20),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 10, WorkItemType = WorkItemType.Epic, ResolvedProductId = 1 },
            new() { WorkItemId = 20, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedEpicId = 10 },
            new() { WorkItemId = 30, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 20 },
        };

        // PBI P had an activity event for the given non-noise field
        var activeWorkItemIds = new HashSet<int> { 30 };

        var featureProgress = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds,
            sprintCompletedPbiIds: new HashSet<int>(), progressMode: FeatureProgressMode.StoryPoints);

        var epicProgress = ComputeEpicProgress(
            featureProgress, resolved, workItems);

        Assert.HasCount(1, epicProgress,
            $"Epic E must appear when PBI has a '{fieldRefName}' change, even if Delivered=0");
    }

    [TestMethod]
    public void EpicVisibility_AcceptanceCriteria2_EpicDoesNotAppearWhenOnlyNoiseActivity()
    {
        // PBI P has ONLY System.ChangedBy / System.ChangedDate revisions during the sprint.
        // These are excluded from the active-work-item set before ComputeFeatureProgress is called.
        // → activeWorkItemIds must NOT contain PBI P → Feature F excluded → Epic E NOT in output.
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [10] = CreateWorkItem(10, WorkItemType.Epic, "Epic E", state: "Active"),
            [20] = CreateWorkItem(20, WorkItemType.Feature, "Feature F", state: "Active"),
            [30] = CreateWorkItem(30, WorkItemType.Pbi, "PBI P", effort: 50, state: "Active", parentId: 20),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 10, WorkItemType = WorkItemType.Epic, ResolvedProductId = 1 },
            new() { WorkItemId = 20, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedEpicId = 10 },
            new() { WorkItemId = 30, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 20 },
        };

        // No non-noise activity: PBI P is NOT in activeWorkItemIds
        // (System.ChangedBy / System.ChangedDate are excluded before the set is built)
        var activeWorkItemIds = new HashSet<int>();

        var featureProgress = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds,
            sprintAssignedPbiIds: new HashSet<int>(), progressMode: FeatureProgressMode.StoryPoints);

        var epicProgress = ComputeEpicProgress(
            featureProgress, resolved, workItems);

        Assert.IsEmpty(featureProgress, "Feature F must be excluded when PBI has only noise-field activity");
        Assert.IsEmpty(epicProgress, "Epic E must NOT appear when only System.ChangedBy/ChangedDate revisions exist");
    }

    [TestMethod]
    public void EpicVisibility_AcceptanceCriteria3_EpicDoesNotAppearWhenNoRevisionsDuringSprint()
    {
        // Epic E and all its descendants have no revisions at all in the sprint window.
        // → activeWorkItemIds is empty and sprintAssignedPbiIds is empty
        // → Feature F excluded → Epic E NOT in output.
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [10] = CreateWorkItem(10, WorkItemType.Epic, "Epic E", state: "Active"),
            [20] = CreateWorkItem(20, WorkItemType.Feature, "Feature F", state: "Active"),
            [30] = CreateWorkItem(30, WorkItemType.Pbi, "PBI P", effort: 50, state: "Active", parentId: 20),
        };

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 10, WorkItemType = WorkItemType.Epic, ResolvedProductId = 1 },
            new() { WorkItemId = 20, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedEpicId = 10 },
            new() { WorkItemId = 30, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 20 },
        };

        // No activity whatsoever, PBI is not sprint-assigned
        var activeWorkItemIds = new HashSet<int>();
        var sprintAssignedPbiIds = new HashSet<int>();

        var featureProgress = ComputeFeatureProgress(
            resolved, workItems, new List<int> { 1 },
            activeWorkItemIds: activeWorkItemIds,
            sprintAssignedPbiIds: sprintAssignedPbiIds, progressMode: FeatureProgressMode.StoryPoints);

        var epicProgress = ComputeEpicProgress(
            featureProgress, resolved, workItems);

        Assert.IsEmpty(epicProgress, "Epic E must NOT appear when it and all descendants have no revisions during the sprint");
    }

    [TestMethod]
    public void EpicVisibility_NoiseExclusion_ChangedByAndChangedDateAreExcludedByProjectionService()
    {
        // Verify that ComputeProductSprintProjection correctly excludes System.ChangedBy and
        // System.ChangedDate from the worked-item set (backs up the acceptance criteria).
        var sprint = CreateSprint(1, "Sprint 1");
        var sprintStart = new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero);

        var resolved = new List<ResolvedWorkItemEntity>
        {
            new() { WorkItemId = 10, WorkItemType = WorkItemType.Epic, ResolvedProductId = 1 },
            new() { WorkItemId = 20, WorkItemType = WorkItemType.Feature, ResolvedProductId = 1, ResolvedSprintId = 1 },
            new() { WorkItemId = 30, WorkItemType = WorkItemType.Pbi, ResolvedProductId = 1, ResolvedFeatureId = 20, ResolvedSprintId = 1 },
        };

        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [10] = CreateWorkItem(10, WorkItemType.Epic, "Epic E", state: "Active"),
            [20] = CreateWorkItem(20, WorkItemType.Feature, "Feature F", state: "Active"),
            [30] = CreateWorkItem(30, WorkItemType.Pbi, "PBI P", effort: 50, state: "Active", parentId: 20),
        };

        // Only noise-field activity on PBI P
        var activity = new Dictionary<int, List<ActivityEventLedgerEntryEntity>>
        {
            [30] = new()
            {
                CreateActivity(30, sprintStart.AddDays(1), "System.ChangedBy"),
                CreateActivity(30, sprintStart.AddDays(1), "System.ChangedDate"),
            }
        };

        var result = ComputeProductSprintProjection(
            sprint, 1, resolved, workItems, activity, sprintStart, sprintEnd);

        Assert.AreEqual(0, result.WorkedCount,
            "System.ChangedBy / System.ChangedDate revisions must not count as worked activity");
        Assert.AreEqual(0, result.CompletedPbiCount, "No PBIs should be completed");
    }

    #endregion

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
        int? parentId = null, DateTimeOffset? createdDate = null,
        int? storyPoints = null, int? businessValue = null)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            Effort = effort,
            StoryPoints = storyPoints ?? effort,
            BusinessValue = businessValue,
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
            EventTimestampUtc = timestamp.UtcDateTime,
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
            EventTimestampUtc = timestamp.UtcDateTime,
        };
    }

    private static ActivityEventLedgerEntryEntity CreateIterationPathActivity(
        int workItemId, DateTimeOffset timestamp, string? oldValue, string? newValue)
    {
        return new ActivityEventLedgerEntryEntity
        {
            WorkItemId = workItemId,
            ProductOwnerId = 1,
            UpdateId = 1,
            FieldRefName = "System.IterationPath",
            EventTimestamp = timestamp,
            EventTimestampUtc = timestamp.UtcDateTime,
            OldValue = oldValue,
            NewValue = newValue
        };
    }

    private static IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification> BuildStateLookup(
        params (string WorkItemType, string StateName, StateClassification Classification)[] classifications)
    {
        return classifications.ToDictionary(
            classification => (classification.WorkItemType.ToCanonicalWorkItemType(), classification.StateName),
            classification => classification.Classification);
    }
}
