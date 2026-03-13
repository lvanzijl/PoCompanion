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
            .ReturnsAsync(new GetStateClassificationsResponse
            {
                ProjectName = "Test",
                IsDefault = false,
                Classifications =
                [
                    new WorkItemStateClassificationDto
                    {
                        WorkItemType = WorkItemType.Pbi,
                        StateName = "Resolved",
                        Classification = StateClassification.Done
                    }
                ]
            });

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

    private GetSprintExecutionQueryHandler CreateHandler()
    {
        return new GetSprintExecutionQueryHandler(
            _context,
            _stateClassificationService.Object,
            NullLogger<GetSprintExecutionQueryHandler>.Instance);
    }

    private async Task SeedSprintExecutionScenarioAsync(string workItemState)
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
            IterationPath = sprint.Path,
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
        await _context.SaveChangesAsync();
    }
}
