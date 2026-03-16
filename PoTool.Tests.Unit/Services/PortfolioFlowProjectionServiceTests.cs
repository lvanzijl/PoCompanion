using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.WorkItems;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Portfolio;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioFlowProjectionServiceTests
{
    [TestMethod]
    public void ComputeProductSprintProjection_ReconstructsStockRemainingAndInflow_ForMidSprintPortfolioEntry()
    {
        var sprint = CreateSprint();
        var portfolioEntryAt = SprintStart(sprint).AddDays(2);
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [101] = CreateWorkItem(101, storyPoints: 8, state: "Active")
        };
        var resolvedItems = new Dictionary<int, ResolvedWorkItemEntity>
        {
            [101] = CreateResolvedWorkItem(101, resolvedProductId: 1)
        };
        var membershipEvents = BuildEventsByWorkItem(
            CreateFieldChange(101, PortfolioEntryLookup.ResolvedProductIdFieldRefName, portfolioEntryAt, null, "1"));

        var projection = CreateService().ComputeProductSprintProjection(
            sprint,
            1,
            new[] { 101 },
            resolvedItems,
            workItems,
            firstDoneByWorkItem: new Dictionary<int, DateTimeOffset>(),
            membershipEventsByWorkItem: membershipEvents);

        Assert.AreEqual(8d, projection.StockStoryPoints, 0.001d);
        Assert.AreEqual(8d, projection.RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(8d, projection.InflowStoryPoints, 0.001d);
        Assert.AreEqual(0d, projection.ThroughputStoryPoints, 0.001d);
        Assert.IsNotNull(projection.CompletionPercent);
        Assert.AreEqual(0d, projection.CompletionPercent.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_CountsInflowAndThroughput_WhenPbiEntersAndCompletesInSameSprint()
    {
        var sprint = CreateSprint();
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [101] = CreateWorkItem(101, storyPoints: 5, state: "Done")
        };
        var resolvedItems = new Dictionary<int, ResolvedWorkItemEntity>
        {
            [101] = CreateResolvedWorkItem(101, resolvedProductId: 1)
        };
        var stateEvents = BuildEventsByWorkItem(
            CreateFieldChange(101, "System.State", SprintStart(sprint).AddDays(4), "Active", "Done"));
        var membershipEvents = BuildEventsByWorkItem(
            CreateFieldChange(101, PortfolioEntryLookup.ResolvedProductIdFieldRefName, SprintStart(sprint).AddDays(1), null, "1"));
        var firstDoneByWorkItem = new SprintCompletionService().BuildFirstDoneByWorkItem(
            stateEvents.SelectMany(pair => pair.Value),
            workItems.Values.ToSnapshotDictionary());

        var projection = CreateService().ComputeProductSprintProjection(
            sprint,
            1,
            new[] { 101 },
            resolvedItems,
            workItems,
            firstDoneByWorkItem: firstDoneByWorkItem,
            stateEventsByWorkItem: stateEvents,
            membershipEventsByWorkItem: membershipEvents);

        Assert.AreEqual(5d, projection.StockStoryPoints, 0.001d);
        Assert.AreEqual(0d, projection.RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(5d, projection.InflowStoryPoints, 0.001d);
        Assert.AreEqual(5d, projection.ThroughputStoryPoints, 0.001d);
        Assert.IsNotNull(projection.CompletionPercent);
        Assert.AreEqual(100d, projection.CompletionPercent.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_UsesFirstDoneForThroughput_WhenPbiIsReopened()
    {
        var sprint = CreateSprint();
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [101] = CreateWorkItem(101, storyPoints: 3, state: "Active")
        };
        var resolvedItems = new Dictionary<int, ResolvedWorkItemEntity>
        {
            [101] = CreateResolvedWorkItem(101, resolvedProductId: 1)
        };
        var stateEvents = BuildEventsByWorkItem(
            CreateFieldChange(101, "System.State", SprintStart(sprint).AddDays(3), "Active", "Done"),
            CreateFieldChange(101, "System.State", SprintStart(sprint).AddDays(5), "Done", "Active"));
        var firstDoneByWorkItem = new SprintCompletionService().BuildFirstDoneByWorkItem(
            stateEvents.SelectMany(pair => pair.Value),
            workItems.Values.ToSnapshotDictionary());

        var projection = CreateService().ComputeProductSprintProjection(
            sprint,
            1,
            new[] { 101 },
            resolvedItems,
            workItems,
            firstDoneByWorkItem: firstDoneByWorkItem,
            stateEventsByWorkItem: stateEvents);

        Assert.AreEqual(3d, projection.StockStoryPoints, 0.001d);
        Assert.AreEqual(3d, projection.RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(3d, projection.ThroughputStoryPoints, 0.001d);
        Assert.IsNotNull(projection.CompletionPercent);
        Assert.AreEqual(0d, projection.CompletionPercent.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_UsesHistoricalEstimateAtFirstDone_AndSprintEndEstimateForStock()
    {
        var sprint = CreateSprint();
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [101] = CreateWorkItem(101, storyPoints: 8, state: "Done")
        };
        var resolvedItems = new Dictionary<int, ResolvedWorkItemEntity>
        {
            [101] = CreateResolvedWorkItem(101, resolvedProductId: 1)
        };
        var stateEvents = BuildEventsByWorkItem(
            CreateFieldChange(101, "System.State", SprintStart(sprint).AddDays(4), "Active", "Done"));
        var storyPointEvents = BuildEventsByWorkItem(
            CreateFieldChange(101, "Microsoft.VSTS.Scheduling.StoryPoints", SprintStart(sprint).AddDays(1), null, "3"),
            CreateFieldChange(101, "Microsoft.VSTS.Scheduling.StoryPoints", SprintStart(sprint).AddDays(3), "3", "5"),
            CreateFieldChange(101, "Microsoft.VSTS.Scheduling.StoryPoints", SprintStart(sprint).AddDays(6), "5", "8"));
        var firstDoneByWorkItem = new SprintCompletionService().BuildFirstDoneByWorkItem(
            stateEvents.SelectMany(pair => pair.Value),
            workItems.Values.ToSnapshotDictionary());

        var projection = CreateService().ComputeProductSprintProjection(
            sprint,
            1,
            new[] { 101 },
            resolvedItems,
            workItems,
            firstDoneByWorkItem: firstDoneByWorkItem,
            stateEventsByWorkItem: stateEvents,
            storyPointEventsByWorkItem: storyPointEvents);

        Assert.AreEqual(8d, projection.StockStoryPoints, 0.001d);
        Assert.AreEqual(5d, projection.ThroughputStoryPoints, 0.001d);
        Assert.IsNotNull(projection.CompletionPercent);
        Assert.AreEqual(100d, projection.CompletionPercent.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_ComputesCompletionPercentFromStockAndRemainingScope()
    {
        var sprint = CreateSprint();
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [101] = CreateWorkItem(101, storyPoints: 6, state: "Done"),
            [102] = CreateWorkItem(102, storyPoints: 2, state: "Active")
        };
        var resolvedItems = new Dictionary<int, ResolvedWorkItemEntity>
        {
            [101] = CreateResolvedWorkItem(101, resolvedProductId: 1, resolvedFeatureId: 501),
            [102] = CreateResolvedWorkItem(102, resolvedProductId: 1, resolvedFeatureId: 501)
        };

        var projection = CreateService().ComputeProductSprintProjection(
            sprint,
            1,
            new[] { 101, 102 },
            resolvedItems,
            workItems);

        Assert.AreEqual(8d, projection.StockStoryPoints, 0.001d);
        Assert.AreEqual(2d, projection.RemainingScopeStoryPoints, 0.001d);
        Assert.IsNotNull(projection.CompletionPercent);
        Assert.AreEqual(75d, projection.CompletionPercent.Value, 0.001d);
    }

    [TestMethod]
    public void ComputeProductSprintProjection_TreatsResolvedProductChangeAsPortfolioInflow_WhenPbiMovesIntoPortfolio()
    {
        var sprint = CreateSprint();
        var movedIntoPortfolioAt = SprintStart(sprint).AddDays(3);
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [101] = CreateWorkItem(101, storyPoints: 8, state: "Active")
        };
        var resolvedItems = new Dictionary<int, ResolvedWorkItemEntity>
        {
            [101] = CreateResolvedWorkItem(101, resolvedProductId: 1)
        };
        var membershipEvents = BuildEventsByWorkItem(
            CreateFieldChange(101, PortfolioEntryLookup.ResolvedProductIdFieldRefName, movedIntoPortfolioAt, "2", "1"));

        var projection = CreateService().ComputeProductSprintProjection(
            sprint,
            1,
            new[] { 101 },
            resolvedItems,
            workItems,
            firstDoneByWorkItem: new Dictionary<int, DateTimeOffset>(),
            membershipEventsByWorkItem: membershipEvents);

        Assert.AreEqual(8d, projection.StockStoryPoints, 0.001d);
        Assert.AreEqual(8d, projection.RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(8d, projection.InflowStoryPoints, 0.001d);
        Assert.AreEqual(0d, projection.ThroughputStoryPoints, 0.001d);
        Assert.IsNotNull(projection.CompletionPercent);
        Assert.AreEqual(0d, projection.CompletionPercent.Value, 0.001d);
    }

    private static PortfolioFlowProjectionService CreateService()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        return new PortfolioFlowProjectionService(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PortfolioFlowProjectionService>.Instance,
            stateClassificationService: null,
            new SprintCompletionService(),
            new CanonicalStoryPointResolutionService());
    }

    private static SprintEntity CreateSprint()
    {
        return new SprintEntity
        {
            Id = 1,
            TeamId = 1,
            Name = "Sprint 1",
            Path = "\\Project\\Sprint 1",
            StartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 1, 14, 23, 59, 59, DateTimeKind.Utc)
        };
    }

    private static DateTimeOffset SprintStart(SprintEntity sprint)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(sprint.StartDateUtc!.Value, DateTimeKind.Utc), TimeSpan.Zero);
    }

    private static WorkItemEntity CreateWorkItem(int workItemId, int? storyPoints, string state)
    {
        return new WorkItemEntity
        {
            TfsId = workItemId,
            Type = WorkItemType.Pbi,
            Title = $"PBI {workItemId}",
            AreaPath = "Area",
            IterationPath = "\\Project\\Sprint 1",
            State = state,
            RetrievedAt = DateTimeOffset.UtcNow,
            StoryPoints = storyPoints,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        };
    }

    private static ResolvedWorkItemEntity CreateResolvedWorkItem(int workItemId, int? resolvedProductId, int? resolvedFeatureId = null)
    {
        return new ResolvedWorkItemEntity
        {
            WorkItemId = workItemId,
            WorkItemType = WorkItemType.Pbi,
            ResolvedProductId = resolvedProductId,
            ResolvedFeatureId = resolvedFeatureId,
            ResolutionStatus = ResolutionStatus.Resolved,
            ResolvedAtRevision = 1
        };
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> BuildEventsByWorkItem(params ActivityEventLedgerEntryEntity[] events)
    {
        return events
            .ToFieldChangeEvents()
            .GroupByWorkItemId();
    }

    private static ActivityEventLedgerEntryEntity CreateFieldChange(
        int workItemId,
        string fieldRefName,
        DateTimeOffset timestamp,
        string? oldValue,
        string? newValue)
    {
        return new ActivityEventLedgerEntryEntity
        {
            Id = Math.Abs(HashCode.Combine(workItemId, fieldRefName, timestamp, oldValue, newValue)),
            ProductOwnerId = 1,
            WorkItemId = workItemId,
            UpdateId = Math.Abs(HashCode.Combine(fieldRefName, timestamp)),
            FieldRefName = fieldRefName,
            EventTimestamp = timestamp,
            EventTimestampUtc = timestamp.UtcDateTime,
            OldValue = oldValue,
            NewValue = newValue
        };
    }
}
