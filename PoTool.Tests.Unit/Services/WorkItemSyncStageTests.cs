using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services.Sync;
using PoTool.Core.Contracts;
using PoTool.Core.Sync;
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

        var tfsClient = CreateTfsClient(workItems);
        var logger = new CapturingLogger<WorkItemSyncStage>();
        var stage = new WorkItemSyncStage(
            tfsClient.Object,
            dbContext,
            new DefaultIncrementalSyncPlanner(),
            logger);

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

    [TestMethod]
    public async Task ExecuteAsync_WithSqliteBaseline_LogsPlannerSummariesAndDebugDump()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var profile = new ProfileEntity { Name = "PO 1" };
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var product = new ProductEntity
        {
            ProductOwnerId = profile.Id,
            Name = "Product 1"
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        dbContext.WorkItems.AddRange(
            CreateWorkItemEntity(1, null),
            CreateWorkItemEntity(2, 1));

        dbContext.ResolvedWorkItems.AddRange(
            CreateResolvedWorkItem(product.Id, 2),
            CreateResolvedWorkItem(product.Id, 999));

        await dbContext.SaveChangesAsync();

        var fetchedWorkItems = Enumerable.Range(1, 23)
            .Select(id => CreateWorkItemDto(id, id == 1 ? null : 1, changedDate: new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero).AddMinutes(id)))
            .ToList();

        var logger = new CapturingLogger<WorkItemSyncStage>(logLevel => logLevel != LogLevel.Trace);
        var tfsClient = CreateTfsClient(fetchedWorkItems);
        var stage = new WorkItemSyncStage(
            tfsClient.Object,
            dbContext,
            new DefaultIncrementalSyncPlanner(),
            logger);

        var result = await stage.ExecuteAsync(
            new SyncContext
            {
                ProductOwnerId = profile.Id,
                RootWorkItemIds = new[] { 1 }
            },
            _ => { },
            CancellationToken.None);

        Assert.IsTrue(result.Success);

        var planLog = logger.Messages.Single(entry => entry.Level == LogLevel.Information && entry.Message.Contains("INCREMENTAL_SYNC_PLAN:", StringComparison.Ordinal));
        StringAssert.Contains(planLog.Message, "EnteredAnalyticalScopeIds=count=22");
        StringAssert.Contains(planLog.Message, "LeftAnalyticalScopeIds=count=1, sample=[999]");
        StringAssert.Contains(planLog.Message, "IdsToHydrate=count=23");
        StringAssert.Contains(planLog.Message, "HierarchyChangedIds=count=23");
        StringAssert.Contains(planLog.Message, "truncated=true");

        var debugLog = logger.Messages.Single(entry => entry.Level == LogLevel.Debug && entry.Message.Contains("INCREMENTAL_SYNC_PLAN_DEBUG:", StringComparison.Ordinal));
        StringAssert.Contains(debugLog.Message, "IdsToHydrate=[1, 2, 3");
        StringAssert.Contains(debugLog.Message, "HierarchyChangedIds=[1, 3, 4");

        var validationLog = logger.Messages.Single(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("INCREMENTAL_SYNC_PLAN_VALIDATION:", StringComparison.Ordinal));
        StringAssert.Contains(validationLog.Message, "ResolvedWorkItemsOutsideClosureScope=count=1, sample=[999]");
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenNoWorkItemsAreFetched_LogsPlannerSkippedWarning()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"WorkItemSyncStageTests_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var logger = new CapturingLogger<WorkItemSyncStage>();
        var tfsClient = CreateTfsClient([]);
        var stage = new WorkItemSyncStage(
            tfsClient.Object,
            dbContext,
            new DefaultIncrementalSyncPlanner(),
            logger);

        var result = await stage.ExecuteAsync(
            new SyncContext
            {
                ProductOwnerId = 1,
                RootWorkItemIds = new[] { 1 },
                WorkItemWatermark = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            _ => { },
            CancellationToken.None);

        Assert.IsTrue(result.Success);

        var skippedLog = logger.Messages.Single(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("INCREMENTAL_SYNC_PLAN_SKIPPED:", StringComparison.Ordinal));
        StringAssert.Contains(skippedLog.Message, "Reason=NoWorkItemsFetched");
    }

    private static WorkItemEntity CreateWorkItemEntity(int tfsId, int? parentTfsId)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            ParentTfsId = parentTfsId,
            Type = "Feature",
            Title = $"Item {tfsId}",
            AreaPath = "Project",
            IterationPath = "Project",
            State = "Active",
            RetrievedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TfsChangedDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TfsChangedDateUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static ResolvedWorkItemEntity CreateResolvedWorkItem(int productId, int workItemId)
    {
        return new ResolvedWorkItemEntity
        {
            WorkItemId = workItemId,
            WorkItemType = "Feature",
            ResolvedProductId = productId,
            ResolutionStatus = ResolutionStatus.Resolved,
            ResolvedAtRevision = 1
        };
    }

    private static WorkItemDto CreateWorkItemDto(int tfsId, int? parentTfsId, DateTimeOffset changedDate)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: "Feature",
            Title: $"Item {tfsId}",
            ParentTfsId: parentTfsId,
            AreaPath: "Project",
            IterationPath: "Project",
            State: "Active",
            RetrievedAt: changedDate.AddMinutes(1),
            Effort: null,
            Description: null,
            ChangedDate: changedDate);
    }

    private static Mock<ITfsClient> CreateTfsClient(IReadOnlyList<WorkItemDto> workItems)
    {
        var tfsClient = new Mock<ITfsClient>();
        tfsClient.Setup(client => client.GetWorkItemsByRootIdsAsync(
                It.IsAny<int[]>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<Action<int, int, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((int[] _, DateTimeOffset? _, Action<int, int, string>? progressCallback, CancellationToken _) =>
                progressCallback?.Invoke(workItems.Count, workItems.Count, "done"))
            .ReturnsAsync(workItems.AsEnumerable());

        return tfsClient;
    }

    private sealed class CapturingLogger<T>(Func<LogLevel, bool>? isEnabled = null) : ILogger<T>
    {
        private static readonly NullScope Scope = new();
        private readonly Func<LogLevel, bool> _isEnabled = isEnabled ?? (_ => true);

        public List<(LogLevel Level, string Message)> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => Scope;

        public bool IsEnabled(LogLevel logLevel)
            => _isEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            Messages.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
