using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Domain.Portfolio;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemResolutionServiceTests
{
    private const string ParentRelationType = "System.LinkTypes.Hierarchy-Reverse";
    private const int TestProductOwnerId = 7;

    [TestMethod]
    public void ResolveAncestry_PbiUnderFeatureUnderEpic_ResolvesCorrectly()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature", parentId: 100),
            [300] = CreateWorkItem(300, WorkItemType.Pbi, "PBI", parentId: 200),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(300, workItems);

        Assert.AreEqual(100, epicId, "EpicId should be 100");
        Assert.AreEqual(200, featureId, "FeatureId should be 200");
    }

    [TestMethod]
    public void ResolveAncestry_FeatureItself_ResolvesFeatureButNotEpic()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature", parentId: 100),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(200, workItems);

        Assert.AreEqual(100, epicId, "EpicId should be 100");
        Assert.AreEqual(200, featureId, "FeatureId should be the feature itself");
    }

    [TestMethod]
    public void ResolveAncestry_EpicItself_ResolvesEpicOnly()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(100, workItems);

        Assert.AreEqual(100, epicId, "EpicId should be the epic itself");
        Assert.IsNull(featureId, "FeatureId should be null for an Epic");
    }

    [TestMethod]
    public void ResolveAncestry_TaskUnderPbiUnderFeature_ResolvesFullChain()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature", parentId: 100),
            [300] = CreateWorkItem(300, WorkItemType.Pbi, "PBI", parentId: 200),
            [400] = CreateWorkItem(400, WorkItemType.Task, "Task", parentId: 300),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(400, workItems);

        Assert.AreEqual(100, epicId, "Task should resolve to Epic 100");
        Assert.AreEqual(200, featureId, "Task should resolve to Feature 200");
    }

    [TestMethod]
    public void ResolveAncestry_OrphanItem_ResolvesNull()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [300] = CreateWorkItem(300, WorkItemType.Pbi, "Orphan PBI"),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(300, workItems);

        Assert.IsNull(epicId, "EpicId should be null for orphan");
        Assert.IsNull(featureId, "FeatureId should be null for orphan");
    }

    [TestMethod]
    public void ResolveAncestry_CircularParentChain_DoesNotInfiniteLoop()
    {
        // Create a circular reference: A -> B -> A
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Pbi, "PBI A", parentId: 200),
            [200] = CreateWorkItem(200, WorkItemType.Pbi, "PBI B", parentId: 100),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(100, workItems);

        // Should not crash; values don't matter, just that it terminates
        Assert.IsNull(epicId);
        Assert.IsNull(featureId);
    }

    [TestMethod]
    public async Task ResolveAllAsync_WhenResolvedProductChanges_PersistsSyntheticLedgerTransition()
    {
        await using var provider = BuildServiceProvider($"WorkItemResolution_Transition_{Guid.NewGuid()}");
        await SeedResolutionScenarioAsync(
            provider,
            seedPreviousResolvedItem: true,
            currentParentId: 200,
            previousResolvedProductId: 1);

        var service = new WorkItemResolutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemResolutionService>.Instance);

        await service.ResolveAllAsync(productOwnerId: TestProductOwnerId);

        await using var verificationScope = provider.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var ledgerEntry = await context.ActivityEventLedgerEntries.SingleAsync();
        var resolvedItem = await context.ResolvedWorkItems.SingleAsync(item => item.WorkItemId == 300);

        Assert.AreEqual(PortfolioEntryLookup.ResolvedProductIdFieldRefName, ledgerEntry.FieldRefName);
        Assert.AreEqual("1", ledgerEntry.OldValue);
        Assert.AreEqual("2", ledgerEntry.NewValue);
        Assert.IsLessThan(0, ledgerEntry.UpdateId);
        Assert.AreEqual(2, resolvedItem.ResolvedProductId);
    }

    [TestMethod]
    public async Task ResolveAllAsync_WhenResolvedProductDoesNotChange_DoesNotPersistSyntheticLedgerTransition()
    {
        await using var provider = BuildServiceProvider($"WorkItemResolution_NoTransition_{Guid.NewGuid()}");
        await SeedResolutionScenarioAsync(
            provider,
            seedPreviousResolvedItem: true,
            currentParentId: 100,
            previousResolvedProductId: 1);

        var service = new WorkItemResolutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemResolutionService>.Instance);

        await service.ResolveAllAsync(productOwnerId: TestProductOwnerId);

        await using var verificationScope = provider.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        Assert.AreEqual(0, await context.ActivityEventLedgerEntries.CountAsync());
        Assert.AreEqual(3, await context.ResolvedWorkItems.CountAsync());
        Assert.AreEqual(1, (await context.ResolvedWorkItems.SingleAsync(item => item.WorkItemId == 300)).ResolvedProductId);
    }

    [TestMethod]
    public async Task ResolveAllAsync_ExcludesOutOfScopeItemsFromLatestClosureSnapshot()
    {
        await using var provider = BuildServiceProvider($"WorkItemResolution_OutOfScope_{Guid.NewGuid()}");
        var olderSnapshot = DateTimeOffset.UtcNow.AddMinutes(-5);
        var latestSnapshot = DateTimeOffset.UtcNow;

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            context.Products.Add(new ProductEntity
            {
                Id = 1,
                ProductOwnerId = TestProductOwnerId,
                Name = "Product A",
                BacklogRoots = [new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 }]
            });

            context.WorkItems.AddRange(
                CreateWorkItem(100, WorkItemType.Feature, "A"),
                CreateWorkItem(200, WorkItemType.Pbi, "B", parentId: 100),
                CreateWorkItem(300, WorkItemType.Pbi, "C", parentId: 100));

            context.ResolvedWorkItems.AddRange(
                new ResolvedWorkItemEntity
                {
                    WorkItemId = 100,
                    WorkItemType = WorkItemType.Feature,
                    ResolvedProductId = 1,
                    ResolvedFeatureId = 100,
                    ResolutionStatus = ResolutionStatus.Resolved,
                    LastResolvedAt = DateTimeOffset.UtcNow,
                    ResolvedAtRevision = 0
                },
                new ResolvedWorkItemEntity
                {
                    WorkItemId = 200,
                    WorkItemType = WorkItemType.Pbi,
                    ResolvedProductId = 1,
                    ResolvedFeatureId = 100,
                    ResolutionStatus = ResolutionStatus.Resolved,
                    LastResolvedAt = DateTimeOffset.UtcNow,
                    ResolvedAtRevision = 0
                },
                new ResolvedWorkItemEntity
                {
                    WorkItemId = 300,
                    WorkItemType = WorkItemType.Pbi,
                    ResolvedProductId = 1,
                    ResolvedFeatureId = 100,
                    ResolutionStatus = ResolutionStatus.Resolved,
                    LastResolvedAt = DateTimeOffset.UtcNow,
                    ResolvedAtRevision = 0
                });

            AddRelationshipSnapshot(context, TestProductOwnerId, olderSnapshot, (200, 100), (300, 100));
            AddRelationshipSnapshot(context, TestProductOwnerId, latestSnapshot, (200, 100));

            await context.SaveChangesAsync();
        }

        var service = new WorkItemResolutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemResolutionService>.Instance);

        await service.ResolveAllAsync(productOwnerId: TestProductOwnerId);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var resolvedIds = await verificationContext.ResolvedWorkItems
            .OrderBy(item => item.WorkItemId)
            .Select(item => item.WorkItemId)
            .ToListAsync();

        CollectionAssert.AreEqual(new List<int> { 100, 200 }, resolvedIds);
    }

    [TestMethod]
    public async Task ResolveAllAsync_ResolvesInScopeHierarchyFromLatestClosureSnapshot()
    {
        await using var provider = BuildServiceProvider($"WorkItemResolution_InScope_{Guid.NewGuid()}");
        var snapshot = DateTimeOffset.UtcNow;

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            context.Products.Add(new ProductEntity
            {
                Id = 1,
                ProductOwnerId = TestProductOwnerId,
                Name = "Product A",
                BacklogRoots = [new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 }]
            });

            context.WorkItems.AddRange(
                CreateWorkItem(100, WorkItemType.Epic, "Epic A"),
                CreateWorkItem(200, WorkItemType.Feature, "Feature B", parentId: 100),
                CreateWorkItem(300, WorkItemType.Pbi, "PBI C", parentId: 200));

            AddRelationshipSnapshot(context, TestProductOwnerId, snapshot, (200, 100), (300, 200));

            await context.SaveChangesAsync();
        }

        var service = new WorkItemResolutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemResolutionService>.Instance);

        var result = await service.ResolveAllAsync(productOwnerId: TestProductOwnerId);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var resolvedPbi = await verificationContext.ResolvedWorkItems.SingleAsync(item => item.WorkItemId == 300);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(3, result.ResolvedCount);
        Assert.AreEqual(100, resolvedPbi.ResolvedEpicId);
        Assert.AreEqual(200, resolvedPbi.ResolvedFeatureId);
    }

    [TestMethod]
    public async Task ResolveAllAsync_IncludesMovedIntoScopeItemsFromLatestClosureSnapshot()
    {
        await using var provider = BuildServiceProvider($"WorkItemResolution_MovedIntoScope_{Guid.NewGuid()}");
        var olderSnapshot = DateTimeOffset.UtcNow.AddMinutes(-5);
        var latestSnapshot = DateTimeOffset.UtcNow;

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            context.Products.Add(new ProductEntity
            {
                Id = 1,
                ProductOwnerId = TestProductOwnerId,
                Name = "Product A",
                BacklogRoots = [new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 }]
            });

            context.WorkItems.AddRange(
                CreateWorkItem(100, WorkItemType.Feature, "A"),
                CreateWorkItem(200, WorkItemType.Pbi, "B", parentId: 100));

            AddRelationshipSnapshot(context, TestProductOwnerId, olderSnapshot);
            AddRelationshipSnapshot(context, TestProductOwnerId, latestSnapshot, (200, 100));

            await context.SaveChangesAsync();
        }

        var service = new WorkItemResolutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemResolutionService>.Instance);

        var result = await service.ResolveAllAsync(productOwnerId: TestProductOwnerId);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var resolvedIds = await verificationContext.ResolvedWorkItems
            .OrderBy(item => item.WorkItemId)
            .Select(item => item.WorkItemId)
            .ToListAsync();

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<int> { 100, 200 }, resolvedIds);
    }

    [TestMethod]
    public async Task ResolveAllAsync_WithSqliteLatestSnapshot_ExecutesWithoutTranslationFailure()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var olderSnapshot = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var latestSnapshot = olderSnapshot.AddMinutes(5);

        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new PoToolDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            setupContext.Profiles.Add(new ProfileEntity
            {
                Id = TestProductOwnerId,
                Name = "PO",
                Products =
                {
                    new ProductEntity
                    {
                        Id = 1,
                        ProductOwnerId = TestProductOwnerId,
                        Name = "Product A",
                        BacklogRoots = [new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 }]
                    }
                }
            });

            setupContext.WorkItems.AddRange(
                CreateWorkItem(100, WorkItemType.Epic, "Epic A"),
                CreateWorkItem(200, WorkItemType.Feature, "Feature B", parentId: 100),
                CreateWorkItem(300, WorkItemType.Pbi, "PBI C", parentId: 200),
                CreateWorkItem(400, WorkItemType.Pbi, "Old Snapshot Only", parentId: 100));

            AddRelationshipSnapshot(setupContext, TestProductOwnerId, olderSnapshot, (200, 100), (400, 100));
            AddRelationshipSnapshot(setupContext, TestProductOwnerId, latestSnapshot, (200, 100), (300, 200));

            await setupContext.SaveChangesAsync();
        }

        await using var provider = new ServiceCollection()
            .AddDbContext<PoToolDbContext>(dbOptions => dbOptions.UseSqlite(connection))
            .BuildServiceProvider();

        var service = new WorkItemResolutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemResolutionService>.Instance);

        var result = await service.ResolveAllAsync(TestProductOwnerId);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var resolvedIds = await verificationContext.ResolvedWorkItems
            .OrderBy(item => item.WorkItemId)
            .Select(item => item.WorkItemId)
            .ToListAsync();
        var resolvedPbi = await verificationContext.ResolvedWorkItems.SingleAsync(item => item.WorkItemId == 300);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(3, result.ResolvedCount);
        CollectionAssert.AreEqual(new List<int> { 100, 200, 300 }, resolvedIds);
        Assert.AreEqual(100, resolvedPbi.ResolvedEpicId);
        Assert.AreEqual(200, resolvedPbi.ResolvedFeatureId);
    }

    private static WorkItemEntity CreateWorkItem(
        int tfsId, string type, string title,
        int? effort = null, string state = "New",
        int? parentId = null)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            Effort = effort,
            State = state,
            ParentTfsId = parentId,
            AreaPath = "\\Project",
            IterationPath = "\\Project\\Sprint 1",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
        };
    }

    private static ServiceProvider BuildServiceProvider(string databaseName)
    {
        return new ServiceCollection()
            .AddDbContext<PoToolDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .BuildServiceProvider();
    }

    private static async Task SeedResolutionScenarioAsync(
        ServiceProvider provider,
        bool seedPreviousResolvedItem,
        int currentParentId,
        int previousResolvedProductId)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        context.Products.AddRange(
            new ProductEntity
            {
                Id = 1,
                ProductOwnerId = TestProductOwnerId,
                Name = "Product A",
                BacklogRoots = [new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 }]
            },
            new ProductEntity
            {
                Id = 2,
                ProductOwnerId = TestProductOwnerId,
                Name = "Product B",
                BacklogRoots = [new ProductBacklogRootEntity { ProductId = 2, WorkItemTfsId = 200 }]
            });

        context.WorkItems.AddRange(
            CreateWorkItem(100, WorkItemType.Feature, "Feature A"),
            CreateWorkItem(200, WorkItemType.Feature, "Feature B"),
            CreateWorkItem(300, WorkItemType.Pbi, "Portfolio PBI", parentId: currentParentId));

        AddRelationshipSnapshot(context, TestProductOwnerId, DateTimeOffset.UtcNow, (300, currentParentId));

        if (seedPreviousResolvedItem)
        {
            context.ResolvedWorkItems.AddRange(
                new ResolvedWorkItemEntity
                {
                    WorkItemId = 100,
                    WorkItemType = WorkItemType.Feature,
                    ResolvedProductId = 1,
                    ResolvedFeatureId = 100,
                    ResolutionStatus = ResolutionStatus.Resolved,
                    LastResolvedAt = DateTimeOffset.UtcNow,
                    ResolvedAtRevision = 0
                },
                new ResolvedWorkItemEntity
                {
                    WorkItemId = 200,
                    WorkItemType = WorkItemType.Feature,
                    ResolvedProductId = 2,
                    ResolvedFeatureId = 200,
                    ResolutionStatus = ResolutionStatus.Resolved,
                    LastResolvedAt = DateTimeOffset.UtcNow,
                    ResolvedAtRevision = 0
                },
                new ResolvedWorkItemEntity
                {
                    WorkItemId = 300,
                    WorkItemType = WorkItemType.Pbi,
                    ResolvedProductId = previousResolvedProductId,
                    ResolvedFeatureId = previousResolvedProductId == 1 ? 100 : 200,
                    ResolutionStatus = ResolutionStatus.Resolved,
                    LastResolvedAt = DateTimeOffset.UtcNow,
                    ResolvedAtRevision = 0
                });
        }

        await context.SaveChangesAsync();
    }

    private static void AddRelationshipSnapshot(
        PoToolDbContext context,
        int productOwnerId,
        DateTimeOffset snapshotAsOfUtc,
        params (int SourceWorkItemId, int TargetWorkItemId)[] parentEdges)
    {
        foreach (var edge in parentEdges)
        {
            context.WorkItemRelationshipEdges.Add(new WorkItemRelationshipEdgeEntity
            {
                ProductOwnerId = productOwnerId,
                SourceWorkItemId = edge.SourceWorkItemId,
                TargetWorkItemId = edge.TargetWorkItemId,
                RelationType = ParentRelationType,
                SnapshotAsOfUtc = snapshotAsOfUtc
            });
        }
    }
}
