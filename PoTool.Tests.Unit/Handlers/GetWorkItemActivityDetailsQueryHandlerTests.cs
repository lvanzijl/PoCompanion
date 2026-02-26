using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Metrics.Queries;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetWorkItemActivityDetailsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private GetWorkItemActivityDetailsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"WorkItemActivityDetails_{Guid.NewGuid()}")
            .Options;

        _context = new PoToolDbContext(options);
        _handler = new GetWorkItemActivityDetailsQueryHandler(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task Handle_ReturnsRootAndChildActivitiesWithinPeriod()
    {
        // Arrange
        var product = new ProductEntity { ProductOwnerId = 5, Name = "Product A", BacklogRootWorkItemId = 1000 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _context.WorkItems.AddRange(
            new WorkItemEntity { TfsId = 2000, ParentTfsId = null, Title = "Epic A", Type = "Epic", AreaPath = "A", IterationPath = "I", State = "Active", RetrievedAt = DateTimeOffset.UtcNow },
            new WorkItemEntity { TfsId = 2100, ParentTfsId = 2000, Title = "Feature A", Type = "Feature", AreaPath = "A", IterationPath = "I", State = "Active", RetrievedAt = DateTimeOffset.UtcNow },
            new WorkItemEntity { TfsId = 2200, ParentTfsId = 2100, Title = "PBI A", Type = "Product Backlog Item", AreaPath = "A", IterationPath = "I", State = "Active", RetrievedAt = DateTimeOffset.UtcNow });

        _context.ResolvedWorkItems.AddRange(
            new ResolvedWorkItemEntity { WorkItemId = 2000, WorkItemType = "Epic", ResolvedProductId = product.Id, ResolutionStatus = ResolutionStatus.Resolved, ResolvedAtRevision = 1 },
            new ResolvedWorkItemEntity { WorkItemId = 2100, WorkItemType = "Feature", ResolvedProductId = product.Id, ResolutionStatus = ResolutionStatus.Resolved, ResolvedAtRevision = 1 },
            new ResolvedWorkItemEntity { WorkItemId = 2200, WorkItemType = "Product Backlog Item", ResolvedProductId = product.Id, ResolutionStatus = ResolutionStatus.Resolved, ResolvedAtRevision = 1 });

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var middle = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        _context.ActivityEventLedgerEntries.AddRange(
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = 5,
                WorkItemId = 2000,
                UpdateId = 1,
                FieldRefName = "System.State",
                EventTimestampUtc = middle,
                EventTimestamp = new DateTimeOffset(middle),
                OldValue = "New",
                NewValue = "Active"
            },
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = 5,
                WorkItemId = 2100,
                UpdateId = 2,
                FieldRefName = "System.ChangedBy",
                EventTimestampUtc = middle,
                EventTimestamp = new DateTimeOffset(middle),
                OldValue = "A",
                NewValue = "B"
            },
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = 5,
                WorkItemId = 2200,
                UpdateId = 3,
                FieldRefName = "System.State",
                EventTimestampUtc = middle.AddDays(1),
                EventTimestamp = new DateTimeOffset(middle.AddDays(1)),
                OldValue = "New",
                NewValue = "Done"
            });
        await _context.SaveChangesAsync();

        var query = new GetWorkItemActivityDetailsQuery(5, 2000, start, start.AddDays(20));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2000, result.WorkItemId);
        Assert.HasCount(2, result.Activities);
        Assert.IsTrue(result.Activities.Any(a => a.WorkItemId == 2000 && a.IsSelectedWorkItem));
        Assert.IsTrue(result.Activities.Any(a => a.WorkItemId == 2200 && !a.IsSelectedWorkItem));
        Assert.IsFalse(result.Activities.Any(a => a.FieldRefName == "System.ChangedBy"));
    }

    [TestMethod]
    public async Task Handle_ReturnsNull_WhenRootWorkItemNotResolvedForProductOwner()
    {
        // Arrange
        _context.Products.Add(new ProductEntity { ProductOwnerId = 3, Name = "Product A", BacklogRootWorkItemId = 1000 });
        _context.WorkItems.Add(new WorkItemEntity { TfsId = 5000, ParentTfsId = null, Title = "Epic A", Type = "Epic", AreaPath = "A", IterationPath = "I", State = "Active", RetrievedAt = DateTimeOffset.UtcNow });
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetWorkItemActivityDetailsQuery(3, 5000, null, null),
            CancellationToken.None);

        // Assert
        Assert.IsNull(result);
    }
}
