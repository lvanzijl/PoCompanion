using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class CacheManagementServiceTests
{
    [TestMethod]
    public async Task GetActivityLedgerValidationAsync_GroupsBySprintAndKeepsUnknownEvents()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"CacheManagementService_Grouping_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        dbContext.ActivityEventLedgerEntries.AddRange(
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = 7,
                WorkItemId = 100,
                UpdateId = 1,
                FieldRefName = "System.ChangedBy",
                EventTimestamp = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
                IterationPath = "Project\\Sprint A",
                NewValue = "alice@example.com"
            },
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = 7,
                WorkItemId = 100,
                UpdateId = 1,
                FieldRefName = "System.State",
                EventTimestamp = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
                IterationPath = "Project\\Sprint A",
                OldValue = "New",
                NewValue = "Active"
            },
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = 7,
                WorkItemId = 100,
                UpdateId = 2,
                FieldRefName = "System.State",
                EventTimestamp = new DateTimeOffset(2026, 2, 2, 10, 0, 0, TimeSpan.Zero),
                IterationPath = "Project\\Unmapped",
                OldValue = "Active",
                NewValue = "Closed"
            });

        await dbContext.SaveChangesAsync();

        var sprintRepository = new Mock<ISprintRepository>(MockBehavior.Strict);
        sprintRepository
            .Setup(x => x.GetAllSprintsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new SprintDto(
                    Id: 11,
                    TeamId: 1,
                    TfsIterationId: "sprint-a",
                    Path: "Project\\Sprint A",
                    Name: "Sprint A",
                    StartUtc: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                    EndUtc: new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero),
                    TimeFrame: "past",
                    LastSyncedUtc: DateTimeOffset.UtcNow)
            ]);

        var service = new CacheManagementService(
            dbContext,
            sprintRepository.Object,
            NullLogger<CacheManagementService>.Instance);

        var result = await service.GetActivityLedgerValidationAsync(7, 100, null, null, CancellationToken.None);

        Assert.HasCount(1, result.SprintGroups);
        Assert.AreEqual("Sprint A", result.SprintGroups[0].SprintName);
        Assert.AreEqual(2, result.SprintGroups[0].TotalEventCount);
        Assert.AreEqual(2, result.SprintGroups[0].DistinctFieldsTouchedCount);
        Assert.AreEqual(1, result.SprintGroups[0].DistinctUsersCount);
        Assert.AreEqual("alice@example.com", result.SprintGroups[0].Events.Single(e => e.FieldRefName == "System.State").ChangedBy);

        Assert.HasCount(1, result.UnknownSprintEvents);
        Assert.AreEqual("Project\\Unmapped", result.UnknownSprintEvents[0].IterationPathAtTime);
    }

    [TestMethod]
    public async Task GetActivityLedgerValidationAsync_ReturnsSnapshotWhenCachedWorkItemExists()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"CacheManagementService_Snapshot_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        dbContext.WorkItems.AddRange(
            new WorkItemEntity
            {
                TfsId = 200,
                ParentTfsId = 201,
                Type = "Product Backlog Item",
                Title = "Child item",
                AreaPath = "Area",
                IterationPath = "Project\\Sprint B",
                State = "Active",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero),
                TfsChangedDateUtc = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)
            },
            new WorkItemEntity
            {
                TfsId = 201,
                Type = "Feature",
                Title = "Parent feature",
                AreaPath = "Area",
                IterationPath = "Project\\Sprint B",
                State = "Active",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            },
            new WorkItemEntity
            {
                TfsId = 202,
                Type = "Feature",
                Title = "Resolved feature",
                AreaPath = "Area",
                IterationPath = "Project\\Sprint B",
                State = "Active",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            },
            new WorkItemEntity
            {
                TfsId = 203,
                Type = "Epic",
                Title = "Resolved epic",
                AreaPath = "Area",
                IterationPath = "Project\\Sprint B",
                State = "Active",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            });

        dbContext.ResolvedWorkItems.Add(new ResolvedWorkItemEntity
        {
            WorkItemId = 200,
            WorkItemType = "Product Backlog Item",
            ResolvedFeatureId = 202,
            ResolvedEpicId = 203,
            ResolutionStatus = ResolutionStatus.Resolved,
            LastResolvedAt = DateTimeOffset.UtcNow,
            ResolvedAtRevision = 1
        });

        dbContext.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
        {
            ProductOwnerId = 7,
            WorkItemId = 200,
            UpdateId = 5,
            FieldRefName = "System.AssignedTo",
            EventTimestamp = new DateTimeOffset(2026, 2, 4, 0, 0, 0, TimeSpan.Zero),
            IterationPath = "Project\\Sprint B",
            NewValue = "owner@example.com"
        });

        await dbContext.SaveChangesAsync();

        var sprintRepository = new Mock<ISprintRepository>(MockBehavior.Strict);
        sprintRepository
            .Setup(x => x.GetAllSprintsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new CacheManagementService(
            dbContext,
            sprintRepository.Object,
            NullLogger<CacheManagementService>.Instance);

        var result = await service.GetActivityLedgerValidationAsync(7, 200, null, null, CancellationToken.None);

        Assert.IsNotNull(result.Snapshot);
        Assert.AreEqual("owner@example.com", result.Snapshot.AssignedTo);
        Assert.AreEqual(201, result.Snapshot.ParentId);
        Assert.AreEqual("Parent feature", result.Snapshot.ParentTitle);
        Assert.AreEqual(202, result.Snapshot.FeatureId);
        Assert.AreEqual("Resolved feature", result.Snapshot.FeatureTitle);
        Assert.AreEqual(203, result.Snapshot.EpicId);
        Assert.AreEqual("Resolved epic", result.Snapshot.EpicTitle);
    }
}
