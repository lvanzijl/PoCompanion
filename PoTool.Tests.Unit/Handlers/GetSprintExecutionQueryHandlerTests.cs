using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetSprintExecutionQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<IWorkItemStateClassificationService> _stateClassificationService = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new PoToolDbContext(options);
        _stateClassificationService = new Mock<IWorkItemStateClassificationService>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task Handle_UsesCanonicalDoneMapping_ForCompletedItems()
    {
        await SeedSprintExecutionScenarioAsync("Resolved");

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse((WorkItemType.Pbi, "Resolved", StateClassification.Done)));

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual(1, result.Summary.CompletedCount, "Resolved PBI should be classified as completed");
        Assert.HasCount(1, result.CompletedPbis);
        Assert.AreEqual(101, result.CompletedPbis[0].TfsId);
        Assert.AreEqual(0, result.Summary.UnfinishedCount);
    }

    [TestMethod]
    public async Task Handle_DoesNotUseRawFallbackDoneStates_WhenClassificationMissing()
    {
        await SeedSprintExecutionScenarioAsync("Closed");

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetStateClassificationsResponse
            {
                ProjectName = "Test",
                IsDefault = false,
                Classifications = []
            });

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual(0, result.Summary.CompletedCount, "Closed should not be treated as done without canonical mapping");
        Assert.AreEqual(1, result.Summary.UnfinishedCount);
        Assert.HasCount(0, result.CompletedPbis);
        Assert.HasCount(1, result.UnfinishedPbis);
        Assert.AreEqual(101, result.UnfinishedPbis[0].TfsId);
    }

    [TestMethod]
    public async Task Handle_DoesNotCountSecondDone_WhenFirstDoneWasBeforeSprint()
    {
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await SeedSprintExecutionScenarioAsync(
            "Resolved",
            stateTransitions:
            [
                (sprintStart.AddDays(-1), "Active", "Resolved"),
                (sprintStart.AddDays(2), "Resolved", "Active"),
                (sprintStart.AddDays(4), "Active", "Resolved")
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse((WorkItemType.Pbi, "Resolved", StateClassification.Done)));

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(0, result.Summary.CompletedCount, "A second Done transition inside the sprint must not count as a new delivery");
        Assert.HasCount(0, result.CompletedPbis);
        Assert.AreEqual(1, result.Summary.UnfinishedCount);
        Assert.AreEqual(101, result.UnfinishedPbis[0].TfsId);
    }

    [TestMethod]
    public async Task Handle_CountsFirstDoneWithinSprint_WhenItemIsNoLongerInSprintIteration()
    {
        await SeedSprintExecutionScenarioAsync(
            "Resolved",
            currentIterationPath: "\\Project\\Backlog",
            stateTransitions:
            [
                (new DateTimeOffset(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)), "Active", "Resolved")
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse((WorkItemType.Pbi, "Resolved", StateClassification.Done)));

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.IsTrue(result.HasData, "Delivery should still appear even when the item is no longer in the current sprint iteration");
        Assert.AreEqual(1, result.Summary.CompletedCount);
        Assert.HasCount(1, result.CompletedPbis);
        Assert.AreEqual(101, result.CompletedPbis[0].TfsId);
        Assert.AreEqual(new DateTimeOffset(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)), result.CompletedPbis[0].ClosedDate);
    }

    [TestMethod]
    public async Task Handle_TreatsItemAddedAfterCommitment_AsAddedScope()
    {
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        const string backlogPath = "\\Project\\Backlog";
        const string sprintPath = "\\Project\\Sprint 1";

        await SeedSprintExecutionScenarioAsync(
            "Active",
            currentIterationPath: sprintPath,
            stateTransitions: [],
            iterationTransitions:
            [
                (sprintStart.AddDays(3), backlogPath, sprintPath)
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse());

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(0, result.Summary.InitialScopeCount, "Items added after commitment should not be counted in initial scope.");
        Assert.AreEqual(1, result.Summary.AddedDuringSprintCount, "Items entering the sprint after commitment should count as added scope.");
        Assert.HasCount(1, result.AddedDuringSprint);
        Assert.AreEqual(101, result.AddedDuringSprint[0].TfsId);
        Assert.AreEqual(sprintStart.AddDays(3), result.AddedDuringSprint[0].EnteredSprintDate);
    }

    [TestMethod]
    public async Task Handle_KeepsCommittedItemInInitialScope_WhenMovedAwayAfterCommitment()
    {
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        const string backlogPath = "\\Project\\Backlog";
        const string sprintPath = "\\Project\\Sprint 1";

        await SeedSprintExecutionScenarioAsync(
            "Active",
            currentIterationPath: backlogPath,
            stateTransitions: [],
            iterationTransitions:
            [
                (sprintStart.AddDays(3), sprintPath, backlogPath)
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse());

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(1, result.Summary.InitialScopeCount, "Committed items should stay in reconstructed initial scope even after moving away.");
        Assert.AreEqual(1, result.Summary.RemovedDuringSprintCount, "Moving away after commitment should count as removed scope.");
        Assert.HasCount(1, result.RemovedDuringSprint);
        Assert.AreEqual(101, result.RemovedDuringSprint[0].TfsId);
        Assert.AreEqual(sprintStart.AddDays(3), result.RemovedDuringSprint[0].RemovedFromSprintDate);
        Assert.IsTrue(result.HasData, "Committed scope reconstruction should keep historical sprint data visible.");
    }

    [TestMethod]
    public async Task Handle_CountsCommittedPbiMovedDirectlyToNextSprint_AsSpillover()
    {
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));
        const string sprintPath = "\\Project\\Sprint 1";
        const string nextSprintPath = "\\Project\\Sprint 2";

        await SeedSprintExecutionScenarioAsync(
            "Active",
            currentIterationPath: nextSprintPath,
            stateTransitions: [],
            iterationTransitions:
            [
                (sprintEnd.AddHours(1), sprintPath, nextSprintPath)
            ],
            additionalSprints:
            [
                (2, "Sprint 2", nextSprintPath, sprintEnd.AddDays(1), sprintEnd.AddDays(14))
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse());

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(1, result.Summary.SpilloverCount);
        Assert.AreEqual(8, result.Summary.SpilloverEffort);
        Assert.HasCount(1, result.SpilloverPbis);
        Assert.AreEqual(101, result.SpilloverPbis[0].TfsId);
        Assert.AreEqual(0, result.Summary.StarvedCount, "Canonical spillover should stay separate from starved work heuristics.");
    }

    [TestMethod]
    public async Task Handle_DoesNotCountBacklogRoundTripToNextSprint_AsSpillover()
    {
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));
        const string sprintPath = "\\Project\\Sprint 1";
        const string backlogPath = "\\Project\\Backlog";
        const string nextSprintPath = "\\Project\\Sprint 2";

        await SeedSprintExecutionScenarioAsync(
            "Active",
            currentIterationPath: nextSprintPath,
            stateTransitions: [],
            iterationTransitions:
            [
                (sprintEnd.AddHours(1), sprintPath, backlogPath),
                (sprintEnd.AddDays(2), backlogPath, nextSprintPath)
            ],
            additionalSprints:
            [
                (2, "Sprint 2", nextSprintPath, sprintEnd.AddDays(1), sprintEnd.AddDays(14))
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse());

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(0, result.Summary.SpilloverCount);
        Assert.HasCount(0, result.SpilloverPbis);
    }

    [TestMethod]
    public async Task Handle_DoesNotCountUnfinishedItemStillOnSprintPath_AsSpillover()
    {
        await SeedSprintExecutionScenarioAsync("Active", stateTransitions: [], iterationTransitions: []);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse());

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(0, result.Summary.SpilloverCount);
        Assert.HasCount(0, result.SpilloverPbis);
        Assert.AreEqual(1, result.Summary.UnfinishedCount, "Items still assigned to the sprint remain unfinished, not spillover.");
    }

    [TestMethod]
    public async Task Handle_ComputesCanonicalStoryPointAggregatesAndRates()
    {
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));
        const string backlogPath = "\\Project\\Backlog";
        const string sprintPath = "\\Project\\Sprint 1";
        const string nextSprintPath = "\\Project\\Sprint 2";

        await SeedSprintExecutionScenarioAsync(
            "Resolved",
            stateTransitions:
            [
                (sprintStart.AddDays(2), "Active", "Resolved")
            ],
            additionalSprints:
            [
                (2, "Sprint 2", nextSprintPath, sprintEnd.AddDays(1), sprintEnd.AddDays(14))
            ],
            additionalWorkItems:
            [
                new SprintExecutionTestWorkItem(
                    102,
                    WorkItemType.Pbi,
                    "Added and delivered",
                    "Resolved",
                    sprintPath,
                    5,
                    null,
                    3,
                    StateTransitions:
                    [
                        (sprintStart.AddDays(4), "Active", "Resolved")
                    ],
                    IterationTransitions:
                    [
                        (sprintStart.AddDays(3), backlogPath, sprintPath)
                    ]),
                new SprintExecutionTestWorkItem(
                    103,
                    WorkItemType.Pbi,
                    "Removed committed scope",
                    "Active",
                    backlogPath,
                    3,
                    2,
                    null,
                    StateTransitions: [],
                    IterationTransitions:
                    [
                        (sprintStart.AddDays(5), sprintPath, backlogPath)
                    ]),
                new SprintExecutionTestWorkItem(
                    104,
                    WorkItemType.Pbi,
                    "Spillover",
                    "Active",
                    nextSprintPath,
                    8,
                    8,
                    null,
                    StateTransitions: [],
                    IterationTransitions:
                    [
                        (sprintEnd.AddHours(1), sprintPath, nextSprintPath)
                    ]),
                new SprintExecutionTestWorkItem(
                    105,
                    WorkItemType.Bug,
                    "Bug with points",
                    "Resolved",
                    sprintPath,
                    2,
                    13,
                    null,
                    StateTransitions:
                    [
                        (sprintStart.AddDays(6), "Active", "Resolved")
                    ]),
                new SprintExecutionTestWorkItem(
                    106,
                    WorkItemType.Task,
                    "Task with points",
                    "Resolved",
                    sprintPath,
                    1,
                    21,
                    null,
                    StateTransitions:
                    [
                        (sprintStart.AddDays(7), "Active", "Resolved")
                    ])
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse(
                (WorkItemType.Pbi, "Resolved", StateClassification.Done),
                (WorkItemType.Bug, "Resolved", StateClassification.Done),
                (WorkItemType.Task, "Resolved", StateClassification.Done)));

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(15d, result.Summary.CommittedSP);
        Assert.AreEqual(3d, result.Summary.AddedSP);
        Assert.AreEqual(2d, result.Summary.RemovedSP);
        Assert.AreEqual(8d, result.Summary.DeliveredSP);
        Assert.AreEqual(3d, result.Summary.DeliveredFromAddedSP);
        Assert.AreEqual(8d, result.Summary.SpilloverSP);
        Assert.AreEqual(5d / 18d, result.Summary.ChurnRate, 1e-9);
        Assert.AreEqual(8d / 13d, result.Summary.CommitmentCompletion, 1e-9);
        Assert.AreEqual(8d / 13d, result.Summary.SpilloverRate, 1e-9);
        Assert.AreEqual(1d, result.Summary.AddedDeliveryRate, 1e-9);
        Assert.IsFalse(result.CompletedPbis.Any(pbi => pbi.TfsId == 106), "Tasks should be excluded from sprint execution story-point scope.");
    }

    [TestMethod]
    public async Task Handle_UsesDerivedEstimatesOnlyForAddedAndRemovedScope()
    {
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        const string backlogPath = "\\Project\\Backlog";
        const string sprintPath = "\\Project\\Sprint 1";

        await SeedSprintExecutionScenarioAsync(
            "Active",
            stateTransitions: [],
            additionalWorkItems:
            [
                new SprintExecutionTestWorkItem(
                    102,
                    WorkItemType.Pbi,
                    "Derived added item",
                    "Active",
                    backlogPath,
                    3,
                    null,
                    null,
                    ParentTfsId: 900,
                    StateTransitions: [],
                    IterationTransitions:
                    [
                        (sprintStart.AddDays(3), backlogPath, sprintPath),
                        (sprintStart.AddDays(5), sprintPath, backlogPath)
                    ]),
                new SprintExecutionTestWorkItem(
                    103,
                    WorkItemType.Pbi,
                    "Sibling estimate",
                    "Active",
                    backlogPath,
                    5,
                    8,
                    null,
                    ParentTfsId: 900,
                    StateTransitions: [])
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse());

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(5d, result.Summary.CommittedSP, "Committed scope should exclude derived estimates.");
        Assert.AreEqual(8d, result.Summary.AddedSP, "Added scope may use derived estimates for scope totals.");
        Assert.AreEqual(8d, result.Summary.RemovedSP, "Removed scope may use derived estimates for scope totals.");
        Assert.AreEqual(0d, result.Summary.DeliveredSP);
        Assert.AreEqual(0d, result.Summary.SpilloverSP, "Spillover remains aligned with committed non-derived scope.");
        Assert.AreEqual(16d / 13d, result.Summary.ChurnRate, 1e-9);
    }

    [TestMethod]
    public async Task Handle_ReturnsZeroStoryPointRates_WhenDenominatorsAreZero()
    {
        await SeedSprintExecutionScenarioAsync(
            "Resolved",
            storyPoints: null,
            businessValue: null,
            additionalWorkItems:
            [
                new SprintExecutionTestWorkItem(
                    102,
                    WorkItemType.Bug,
                    "Bug only",
                    "Resolved",
                    "\\Project\\Sprint 1",
                    2,
                    13,
                    null,
                    StateTransitions:
                    [
                        (new DateTimeOffset(new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)), "Active", "Resolved")
                    ])
            ]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse(
                (WorkItemType.Pbi, "Resolved", StateClassification.Done),
                (WorkItemType.Bug, "Resolved", StateClassification.Done)));

        var handler = CreateHandler();

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.AreEqual(0d, result.Summary.CommittedSP);
        Assert.AreEqual(0d, result.Summary.AddedSP);
        Assert.AreEqual(0d, result.Summary.RemovedSP);
        Assert.AreEqual(0d, result.Summary.DeliveredSP);
        Assert.AreEqual(0d, result.Summary.DeliveredFromAddedSP);
        Assert.AreEqual(0d, result.Summary.SpilloverSP);
        Assert.AreEqual(0d, result.Summary.ChurnRate);
        Assert.AreEqual(0d, result.Summary.CommitmentCompletion);
        Assert.AreEqual(0d, result.Summary.SpilloverRate);
        Assert.AreEqual(0d, result.Summary.AddedDeliveryRate);
    }

    private GetSprintExecutionQueryHandler CreateHandler()
    {
        return new GetSprintExecutionQueryHandler(
            _context,
            _stateClassificationService.Object,
            new CanonicalStoryPointResolutionService(),
            NullLogger<GetSprintExecutionQueryHandler>.Instance);
    }

    private async Task SeedSprintExecutionScenarioAsync(
        string workItemState,
        string? currentIterationPath = null,
        int? storyPoints = 5,
        int? businessValue = null,
        IEnumerable<(DateTimeOffset Timestamp, string OldState, string NewState)>? stateTransitions = null,
        IEnumerable<(DateTimeOffset Timestamp, string? OldIterationPath, string? NewIterationPath)>? iterationTransitions = null,
        IEnumerable<(int Id, string Name, string Path, DateTimeOffset StartUtc, DateTimeOffset EndUtc)>? additionalSprints = null,
        IEnumerable<SprintExecutionTestWorkItem>? additionalWorkItems = null)
    {
        var profile = new ProfileEntity
        {
            Id = 1,
            Name = "PO"
        };

        var team = new TeamEntity
        {
            Id = 1,
            Name = "Team",
            TeamAreaPath = "\\Project\\Team"
        };

        var product = new ProductEntity
        {
            Id = 1,
            ProductOwnerId = profile.Id,
            Name = "Product"
        };

        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));

        var sprint = new SprintEntity
        {
            Id = 1,
            TeamId = team.Id,
            Name = "Sprint 1",
            Path = "\\Project\\Sprint 1",
            StartUtc = sprintStart,
            StartDateUtc = sprintStart.UtcDateTime,
            EndUtc = sprintEnd,
            EndDateUtc = sprintEnd.UtcDateTime,
            LastSyncedUtc = DateTimeOffset.UtcNow,
            LastSyncedDateUtc = DateTime.UtcNow
        };

        var workItem = new WorkItemEntity
        {
            TfsId = 101,
            Type = WorkItemType.Pbi,
            Title = "PBI 101",
            ParentTfsId = null,
            AreaPath = "\\Project",
            IterationPath = currentIterationPath ?? sprint.Path,
            State = workItemState,
            Effort = 8,
            StoryPoints = storyPoints,
            BusinessValue = businessValue,
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        };

        var resolved = new ResolvedWorkItemEntity
        {
            WorkItemId = 101,
            WorkItemType = WorkItemType.Pbi,
            ResolvedProductId = product.Id,
            ResolvedSprintId = sprint.Id,
            ResolutionStatus = ResolutionStatus.Resolved,
            ResolvedAtRevision = 1
        };

        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Products.Add(product);
        _context.Sprints.Add(sprint);

        foreach (var (id, name, path, startUtc, endUtc) in additionalSprints ?? [])
        {
            _context.Sprints.Add(new SprintEntity
            {
                Id = id,
                TeamId = team.Id,
                Name = name,
                Path = path,
                StartUtc = startUtc,
                StartDateUtc = startUtc.UtcDateTime,
                EndUtc = endUtc,
                EndDateUtc = endUtc.UtcDateTime,
                LastSyncedUtc = DateTimeOffset.UtcNow,
                LastSyncedDateUtc = DateTime.UtcNow
            });
        }

        _context.WorkItems.Add(workItem);
        _context.ResolvedWorkItems.Add(resolved);
        var updateId = 1;

        AddStateTransitions(workItem.TfsId, profile.Id, stateTransitions ?? [(sprintStart.AddDays(2), "Active", workItemState)], ref updateId);
        AddIterationTransitions(workItem.TfsId, profile.Id, iterationTransitions ?? [], ref updateId);

        foreach (var additionalWorkItem in additionalWorkItems ?? [])
        {
            _context.WorkItems.Add(new WorkItemEntity
            {
                TfsId = additionalWorkItem.TfsId,
                Type = additionalWorkItem.Type,
                Title = additionalWorkItem.Title,
                ParentTfsId = additionalWorkItem.ParentTfsId,
                AreaPath = "\\Project",
                IterationPath = additionalWorkItem.CurrentIterationPath ?? sprint.Path,
                State = additionalWorkItem.State,
                Effort = additionalWorkItem.Effort,
                StoryPoints = additionalWorkItem.StoryPoints,
                BusinessValue = additionalWorkItem.BusinessValue,
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            });

            _context.ResolvedWorkItems.Add(new ResolvedWorkItemEntity
            {
                WorkItemId = additionalWorkItem.TfsId,
                WorkItemType = additionalWorkItem.Type,
                ResolvedProductId = product.Id,
                ResolvedSprintId = sprint.Id,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            });

            AddStateTransitions(
                additionalWorkItem.TfsId,
                profile.Id,
                additionalWorkItem.StateTransitions ?? [(sprintStart.AddDays(2), "Active", additionalWorkItem.State)],
                ref updateId);
            AddIterationTransitions(
                additionalWorkItem.TfsId,
                profile.Id,
                additionalWorkItem.IterationTransitions ?? [],
                ref updateId);
        }

        await _context.SaveChangesAsync();
    }

    private void AddStateTransitions(
        int workItemId,
        int productOwnerId,
        IEnumerable<(DateTimeOffset Timestamp, string OldState, string NewState)> transitions,
        ref int updateId)
    {
        foreach (var (timestamp, oldState, newState) in transitions)
        {
            _context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = workItemId,
                UpdateId = updateId++,
                FieldRefName = "System.State",
                EventTimestamp = timestamp,
                EventTimestampUtc = timestamp.UtcDateTime,
                OldValue = oldState,
                NewValue = newState
            });
        }
    }

    private void AddIterationTransitions(
        int workItemId,
        int productOwnerId,
        IEnumerable<(DateTimeOffset Timestamp, string? OldIterationPath, string? NewIterationPath)> transitions,
        ref int updateId)
    {
        foreach (var (timestamp, oldIterationPath, newIterationPath) in transitions)
        {
            _context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = workItemId,
                UpdateId = updateId++,
                FieldRefName = "System.IterationPath",
                EventTimestamp = timestamp,
                EventTimestampUtc = timestamp.UtcDateTime,
                OldValue = oldIterationPath,
                NewValue = newIterationPath
            });
        }
    }

    private static GetStateClassificationsResponse BuildStateClassificationsResponse(
        params (string WorkItemType, string StateName, StateClassification Classification)[] classifications)
    {
        return new GetStateClassificationsResponse
        {
            ProjectName = "Test",
            IsDefault = false,
            Classifications = classifications.Select(classification => new WorkItemStateClassificationDto
            {
                WorkItemType = classification.WorkItemType,
                StateName = classification.StateName,
                Classification = classification.Classification
            }).ToList()
        };
    }

    private sealed record SprintExecutionTestWorkItem(
        int TfsId,
        string Type,
        string Title,
        string State,
        string? CurrentIterationPath,
        int? Effort,
        int? StoryPoints,
        int? BusinessValue,
        int? ParentTfsId = null,
        IEnumerable<(DateTimeOffset Timestamp, string OldState, string NewState)>? StateTransitions = null,
        IEnumerable<(DateTimeOffset Timestamp, string? OldIterationPath, string? NewIterationPath)>? IterationTransitions = null);
}
