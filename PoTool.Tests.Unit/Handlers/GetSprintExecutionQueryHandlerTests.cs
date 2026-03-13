using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
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

    private GetSprintExecutionQueryHandler CreateHandler()
    {
        return new GetSprintExecutionQueryHandler(
            _context,
            _stateClassificationService.Object,
            NullLogger<GetSprintExecutionQueryHandler>.Instance);
    }

    private async Task SeedSprintExecutionScenarioAsync(
        string workItemState,
        string? currentIterationPath = null,
        IEnumerable<(DateTimeOffset Timestamp, string OldState, string NewState)>? stateTransitions = null)
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
            AreaPath = "\\Project",
            IterationPath = currentIterationPath ?? sprint.Path,
            State = workItemState,
            Effort = 8,
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
        _context.WorkItems.Add(workItem);
        _context.ResolvedWorkItems.Add(resolved);
        foreach (var (timestamp, oldState, newState) in stateTransitions
                     ?? [(sprintStart.AddDays(2), "Active", workItemState)])
        {
            _context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = profile.Id,
                WorkItemId = workItem.TfsId,
                UpdateId = 1,
                FieldRefName = "System.State",
                EventTimestamp = timestamp,
                EventTimestampUtc = timestamp.UtcDateTime,
                OldValue = oldState,
                NewValue = newState
            });
        }

        await _context.SaveChangesAsync();
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
}
