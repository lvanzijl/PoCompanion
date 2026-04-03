using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Api.Services.Sync;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SyncPipelineRunnerTests
{
    private const string ParentRelationType = "System.LinkTypes.Hierarchy-Reverse";
    private const int TestProductOwnerId = 7;

    [TestMethod]
    public async Task ExecuteAsync_WhenSnapshotSaveFails_StopsBeforeResolution_AndPreservesPreviousSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var snapshotFailureSwitch = new SnapshotSaveFailureSwitch();
        var tfsClient = CreateTfsClient();
        var planner = new Mock<IIncrementalSyncPlanner>();
        planner.Setup(service => service.Plan(It.IsAny<IncrementalSyncPlannerRequest>()))
            .Returns(new IncrementalSyncPlan
            {
                PlanningMode = IncrementalSyncPlanningMode.Full,
                AnalyticalScopeIds = [100, 200],
                ClosureScopeIds = [100, 200],
                RequiresRelationshipSnapshotRebuild = true,
                RequiresResolutionRebuild = true
            });

        var sprintRepository = new Mock<ISprintRepository>(MockBehavior.Strict);

        await using var provider = BuildServiceProvider(connection, snapshotFailureSwitch, tfsClient.Object, planner.Object, sprintRepository.Object);
        await SeedSnapshotFailureScenarioAsync(provider);

        snapshotFailureSwitch.ThrowOnNextSnapshotSave = true;

        var runner = provider.GetRequiredService<SyncPipelineRunner>();
        var updates = new List<SyncProgressUpdate>();

        await foreach (var update in runner.ExecuteAsync(TestProductOwnerId))
        {
            updates.Add(update);
        }

        await using var verificationScope = provider.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var cacheState = await context.ProductOwnerCacheStates.SingleAsync(state => state.ProductOwnerId == TestProductOwnerId);
        var relationshipEdges = await context.WorkItemRelationshipEdges
            .Where(edge => edge.ProductOwnerId == TestProductOwnerId)
            .OrderBy(edge => edge.SourceWorkItemId)
            .ToListAsync();
        var resolvedCount = await context.ResolvedWorkItems.CountAsync();

        Assert.IsTrue(updates.Any(update => update.CurrentStage == "SyncWorkItemRelationships" && update.HasFailed));
        Assert.IsFalse(updates.Any(update => update.CurrentStage == "ResolveWorkItems"), "Resolution must not run after snapshot failure.");
        Assert.AreEqual(CacheSyncStatus.Failed, cacheState.SyncStatus, "Snapshot failure must remain a failed sync, not partial success.");
        Assert.HasCount(1, relationshipEdges, "The previous relationship snapshot should remain intact after a failed replacement.");
        Assert.AreEqual(200, relationshipEdges[0].SourceWorkItemId);
        Assert.AreEqual(100, relationshipEdges[0].TargetWorkItemId);
        Assert.AreEqual(0, resolvedCount, "No resolved rows should be rebuilt when snapshot capture fails.");
    }

    private static Mock<ITfsClient> CreateTfsClient()
    {
        var workItems = new[]
        {
            CreateWorkItemDto(100, WorkItemType.Feature, "Feature A"),
            CreateWorkItemDto(200, WorkItemType.Pbi, "PBI B", parentId: 100),
        };

        var tfsClient = new Mock<ITfsClient>(MockBehavior.Strict);
        tfsClient.SetupSequence(client => client.GetWorkItemsByRootIdsAsync(
                It.IsAny<int[]>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<Action<int, int, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems)
            .ReturnsAsync(workItems);
        tfsClient.Setup(client => client.GetWorkItemUpdatesAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return tfsClient;
    }

    private static async Task SeedSnapshotFailureScenarioAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        await context.Database.EnsureCreatedAsync();

        var previousSnapshot = DateTimeOffset.UtcNow.AddDays(-1);

        context.Profiles.Add(new ProfileEntity
        {
            Id = TestProductOwnerId,
            Name = "PO",
            Products =
            {
                new ProductEntity
                {
                    Id = 1,
                    ProductOwnerId = TestProductOwnerId,
                    ProjectId = PersistenceTestGraph.DefaultProjectId,
                    Name = "Product A",
                    BacklogRoots = [new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 }]
                }
            }
        });
        PersistenceTestGraph.EnsureProject(context);

        context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = TestProductOwnerId,
            SyncStatus = CacheSyncStatus.Idle,
            RelationshipsSnapshotAsOfUtc = previousSnapshot,
            RelationshipsSnapshotWorkItemWatermark = previousSnapshot
        });

        context.WorkItemRelationshipEdges.Add(new WorkItemRelationshipEdgeEntity
        {
            ProductOwnerId = TestProductOwnerId,
            SourceWorkItemId = 200,
            TargetWorkItemId = 100,
            RelationType = ParentRelationType,
            SnapshotAsOfUtc = previousSnapshot
        });

        await context.SaveChangesAsync();
    }

    private static ServiceProvider BuildServiceProvider(
        SqliteConnection connection,
        SnapshotSaveFailureSwitch snapshotFailureSwitch,
        ITfsClient tfsClient,
        IIncrementalSyncPlanner planner,
        ISprintRepository sprintRepository)
    {
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(snapshotFailureSwitch)
            .AddScoped<PoToolDbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<PoToolDbContext>()
                    .UseSqlite(connection)
                    .Options;
                return new ThrowingSnapshotSaveDbContext(options, snapshotFailureSwitch);
            })
            .AddScoped<ICacheStateRepository, CacheStateRepository>()
            .AddScoped(_ => tfsClient)
            .AddScoped(_ => planner)
            .AddScoped(_ => sprintRepository)
            .Configure<ActivityIngestionOptions>(options => options.ActivityBackfillDays = 0)
            .AddScoped<ActivityEventIngestionService>()
            .AddScoped<WorkItemSyncStage>()
            .AddScoped<ActivityIngestionSyncStage>()
            .AddScoped<TeamSprintSyncStage>()
            .AddSingleton<WorkItemRelationshipSnapshotService>()
            .AddScoped<WorkItemRelationshipSnapshotStage>()
            .AddSingleton<WorkItemResolutionService>()
            .AddScoped<WorkItemResolutionSyncStage>()
            .AddSingleton<SyncPipelineRunner>()
            .BuildServiceProvider();
    }

    private static WorkItemDto CreateWorkItemDto(
        int tfsId,
        string type,
        string title,
        int? parentId = null)
    {
        var changedDate = DateTimeOffset.UtcNow;
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: parentId,
            AreaPath: "\\Project",
            IterationPath: "\\Project\\Sprint 1",
            State: "New",
            RetrievedAt: changedDate,
            Effort: null,
            Description: null,
            ChangedDate: changedDate);
    }

    private sealed class SnapshotSaveFailureSwitch
    {
        public bool ThrowOnNextSnapshotSave { get; set; }
    }

    private sealed class ThrowingSnapshotSaveDbContext : PoToolDbContext
    {
        private readonly SnapshotSaveFailureSwitch _snapshotFailureSwitch;

        public ThrowingSnapshotSaveDbContext(
            DbContextOptions<PoToolDbContext> options,
            SnapshotSaveFailureSwitch snapshotFailureSwitch)
            : base(options)
        {
            _snapshotFailureSwitch = snapshotFailureSwitch;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_snapshotFailureSwitch.ThrowOnNextSnapshotSave &&
                ChangeTracker.Entries<WorkItemRelationshipEdgeEntity>().Any(entry => entry.State == EntityState.Added))
            {
                _snapshotFailureSwitch.ThrowOnNextSnapshotSave = false;
                throw new InvalidOperationException("Simulated snapshot save failure.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
