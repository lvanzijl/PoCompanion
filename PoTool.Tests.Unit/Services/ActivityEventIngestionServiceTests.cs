using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class ActivityEventIngestionServiceTests
{
    [TestMethod]
    public async Task IngestAsync_WhenRunTwice_DoesNotDuplicateEvents()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ActivityEventIngestion_Idempotent_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        PersistenceTestGraph.EnsureProfile(dbContext, 1, "PO 1");
        dbContext.WorkItems.Add(new WorkItemEntity
        {
            TfsId = 101,
            Type = "PBI",
            Title = "Work Item",
            AreaPath = "Area",
            IterationPath = "Iteration A",
            State = "New",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        });
        dbContext.ProductOwnerCacheStates.Add(PersistenceTestGraph.CreateCacheState(1));
        await dbContext.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var tfsClient = new Mock<ITfsClient>(MockBehavior.Strict);
        tfsClient.Setup(client => client.GetWorkItemUpdatesAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WorkItemUpdate
                {
                    WorkItemId = 101,
                    UpdateId = 7,
                    RevisedDate = now,
                    FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["System.State"] = new("System.State", "New", "Active"),
                        ["System.AssignedTo"] = new("System.AssignedTo", "A", "B")
                    }
                }
            ]);

        var service = new ActivityEventIngestionService(
            dbContext,
            tfsClient.Object,
            Options.Create(new ActivityIngestionOptions { ActivityBackfillDays = 0 }),
            NullLogger<ActivityEventIngestionService>.Instance);

        await service.IngestAsync(1);
        await service.IngestAsync(1);

        var events = await dbContext.ActivityEventLedgerEntries.ToListAsync();
        Assert.HasCount(1, events);
        Assert.AreEqual("System.State", events[0].FieldRefName);
        Assert.IsFalse(events.Any(e => string.Equals(e.FieldRefName, "System.AssignedTo", StringComparison.OrdinalIgnoreCase)));

        var cacheState = await dbContext.ProductOwnerCacheStates.SingleAsync(x => x.ProductOwnerId == 1);
        Assert.IsTrue(cacheState.ActivityEventWatermark.HasValue);
    }

    [TestMethod]
    public async Task IngestAsync_BackfillDays_FiltersOutOlderUpdates()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ActivityEventIngestion_Backfill_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        PersistenceTestGraph.EnsureProfile(dbContext, 1, "PO 1");
        dbContext.WorkItems.Add(new WorkItemEntity
        {
            TfsId = 202,
            Type = "PBI",
            Title = "Work Item",
            AreaPath = "Area",
            IterationPath = "Iteration A",
            State = "New",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        });
        dbContext.ProductOwnerCacheStates.Add(PersistenceTestGraph.CreateCacheState(1));
        await dbContext.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var tfsClient = new Mock<ITfsClient>(MockBehavior.Strict);
        tfsClient.Setup(client => client.GetWorkItemUpdatesAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WorkItemUpdate
                {
                    WorkItemId = 202,
                    UpdateId = 1,
                    RevisedDate = now.AddDays(-20),
                    FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>
                    {
                        ["System.State"] = new("System.State", "New", "Active")
                    }
                },
                new WorkItemUpdate
                {
                    WorkItemId = 202,
                    UpdateId = 2,
                    RevisedDate = now.AddDays(-1),
                    FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>
                    {
                        ["System.State"] = new("System.State", "Active", "Closed")
                    }
                }
            ]);

        var service = new ActivityEventIngestionService(
            dbContext,
            tfsClient.Object,
            Options.Create(new ActivityIngestionOptions { ActivityBackfillDays = 7 }),
            NullLogger<ActivityEventIngestionService>.Instance);

        var result = await service.IngestAsync(1);
        var events = await dbContext.ActivityEventLedgerEntries.ToListAsync();

        Assert.HasCount(1, events);
        Assert.AreEqual(2, events[0].UpdateId);
        Assert.AreEqual(now.AddDays(-1).ToUnixTimeSeconds(), result.Watermark?.ToUnixTimeSeconds());
    }

    [TestMethod]
    public async Task IngestAsync_StoryPointsChange_PersistsLedgerRow()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ActivityEventIngestion_StoryPoints_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        PersistenceTestGraph.EnsureProfile(dbContext, 1, "PO 1");
        dbContext.WorkItems.Add(new WorkItemEntity
        {
            TfsId = 303,
            Type = "PBI",
            Title = "Estimated Work Item",
            AreaPath = "Area",
            IterationPath = "Iteration A",
            State = "New",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        });
        dbContext.ProductOwnerCacheStates.Add(PersistenceTestGraph.CreateCacheState(1));
        await dbContext.SaveChangesAsync();

        var revisedDate = DateTimeOffset.UtcNow;
        var tfsClient = new Mock<ITfsClient>(MockBehavior.Strict);
        tfsClient.Setup(client => client.GetWorkItemUpdatesAsync(303, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WorkItemUpdate
                {
                    WorkItemId = 303,
                    UpdateId = 11,
                    RevisedDate = revisedDate,
                    FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Microsoft.VSTS.Scheduling.StoryPoints"] = new("Microsoft.VSTS.Scheduling.StoryPoints", "3", "5")
                    }
                }
            ]);

        var service = new ActivityEventIngestionService(
            dbContext,
            tfsClient.Object,
            Options.Create(new ActivityIngestionOptions { ActivityBackfillDays = 0 }),
            NullLogger<ActivityEventIngestionService>.Instance);

        await service.IngestAsync(1);
        await service.IngestAsync(1);

        var ledgerRow = await dbContext.ActivityEventLedgerEntries.SingleAsync();
        Assert.AreEqual("Microsoft.VSTS.Scheduling.StoryPoints", ledgerRow.FieldRefName);
        Assert.AreEqual("3", ledgerRow.OldValue);
        Assert.AreEqual("5", ledgerRow.NewValue);
        Assert.AreEqual(11, ledgerRow.UpdateId);
    }

    [TestMethod]
    public async Task IngestAsync_PhaseACorrectionFields_ArePersistedToActivityLedger()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ActivityEventIngestion_PhaseACorrections_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        PersistenceTestGraph.EnsureProfile(dbContext, 1, "PO 1");
        dbContext.WorkItems.Add(new WorkItemEntity
        {
            TfsId = 404,
            Type = "Epic",
            Title = "Funded Epic",
            AreaPath = "Area",
            IterationPath = "Iteration A",
            State = "New",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        });
        dbContext.ProductOwnerCacheStates.Add(PersistenceTestGraph.CreateCacheState(1));
        await dbContext.SaveChangesAsync();

        var revisedDate = DateTimeOffset.UtcNow;
        var tfsClient = new Mock<ITfsClient>(MockBehavior.Strict);
        tfsClient.Setup(client => client.GetWorkItemUpdatesAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WorkItemUpdate
                {
                    WorkItemId = 404,
                    UpdateId = 12,
                    RevisedDate = revisedDate,
                    FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Rhodium.Funding.ProjectNumber"] = new("Rhodium.Funding.ProjectNumber", "PRJ-1", "PRJ-2"),
                        ["Rhodium.Funding.ProjectElement"] = new("Rhodium.Funding.ProjectElement", "ELM-1", "ELM-2"),
                        ["Microsoft.VSTS.Common.TimeCriticality"] = new("Microsoft.VSTS.Common.TimeCriticality", "25", "50")
                    }
                }
            ]);

        var service = new ActivityEventIngestionService(
            dbContext,
            tfsClient.Object,
            Options.Create(new ActivityIngestionOptions { ActivityBackfillDays = 0 }),
            NullLogger<ActivityEventIngestionService>.Instance);

        await service.IngestAsync(1);

        var events = await dbContext.ActivityEventLedgerEntries
            .OrderBy(entry => entry.FieldRefName)
            .ToListAsync();

        Assert.HasCount(3, events);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "Microsoft.VSTS.Common.TimeCriticality",
                "Rhodium.Funding.ProjectElement",
                "Rhodium.Funding.ProjectNumber"
            },
            events.Select(entry => entry.FieldRefName).ToArray());
    }
}
