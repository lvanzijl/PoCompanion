using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Services.Sync;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemSyncStageTests
{
    [TestMethod]
    public async Task ExecuteAsync_UsesChangedDateForWatermarkAndPersistence()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"WorkItemSyncStageTests_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var workItems = new List<WorkItemDto>
        {
            new(
                TfsId: 1,
                Type: "Feature",
                Title: "Item 1",
                ParentTfsId: null,
                AreaPath: "Project",
                IterationPath: "Project",
                State: "Active",
                RetrievedAt: new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero),
                Effort: null,
                Description: null,
                ChangedDate: new DateTimeOffset(2025, 1, 1, 8, 0, 0, TimeSpan.Zero)
            ),
            new(
                TfsId: 2,
                Type: "Feature",
                Title: "Item 2",
                ParentTfsId: null,
                AreaPath: "Project",
                IterationPath: "Project",
                State: "Active",
                RetrievedAt: new DateTimeOffset(2025, 1, 2, 10, 0, 0, TimeSpan.Zero),
                Effort: null,
                Description: null,
                ChangedDate: new DateTimeOffset(2025, 1, 2, 9, 0, 0, TimeSpan.Zero)
            )
        };

        var tfsClient = new Mock<ITfsClient>();
        tfsClient.Setup(client => client.GetWorkItemsByRootIdsAsync(
                It.IsAny<int[]>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<Action<int, int, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var logger = new Mock<ILogger<WorkItemSyncStage>>();
        var stage = new WorkItemSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = new[] { 1 }
        };

        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(workItems[1].ChangedDate, result.NewWatermark);

        var entity = await dbContext.WorkItems.FirstAsync(w => w.TfsId == 1);
        Assert.AreEqual(workItems[0].ChangedDate, entity.TfsChangedDate);
        Assert.AreEqual(workItems[0].ChangedDate?.UtcDateTime, entity.TfsChangedDateUtc);
    }
}
