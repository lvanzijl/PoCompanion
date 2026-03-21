using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemRelationshipSnapshotServiceTests
{
    private const int TestProductOwnerId = 7;

    [TestMethod]
    public async Task BuildSnapshotAsync_ReplacesPreviousRunEdges_AndResolutionUsesOnlyCurrentRunGraph()
    {
        var run1RetrievedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var run2RetrievedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var run1WorkItems = new[]
        {
            CreateWorkItemDto(100, WorkItemType.Feature, "A", run1RetrievedAt),
            CreateWorkItemDto(200, WorkItemType.Pbi, "B", run1RetrievedAt, parentId: 100),
            CreateWorkItemDto(300, WorkItemType.Pbi, "C", run1RetrievedAt, parentId: 200),
        };
        var run2WorkItems = new[]
        {
            CreateWorkItemDto(100, WorkItemType.Feature, "A", run2RetrievedAt),
            CreateWorkItemDto(200, WorkItemType.Pbi, "B", run2RetrievedAt, parentId: 100),
        };

        var tfsClient = new Mock<ITfsClient>(MockBehavior.Strict);
        tfsClient.SetupSequence(client => client.GetWorkItemsByRootIdsAsync(
                It.IsAny<int[]>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(run1WorkItems)
            .ReturnsAsync(run2WorkItems);

        await using var provider = BuildServiceProvider($"RelationshipSnapshot_{Guid.NewGuid()}", tfsClient.Object);

        await SeedProfileAndPersistedWorkItemsAsync(provider);

        var snapshotService = new WorkItemRelationshipSnapshotService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemRelationshipSnapshotService>.Instance);
        var resolutionService = new WorkItemResolutionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemResolutionService>.Instance);

        var run1Result = await snapshotService.BuildSnapshotAsync(TestProductOwnerId);
        var run2Result = await snapshotService.BuildSnapshotAsync(TestProductOwnerId);
        var resolutionResult = await resolutionService.ResolveAllAsync(TestProductOwnerId);

        await using var verificationScope = provider.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var relationshipEdges = await context.WorkItemRelationshipEdges
            .Where(edge => edge.ProductOwnerId == TestProductOwnerId)
            .OrderBy(edge => edge.SourceWorkItemId)
            .ToListAsync();
        var resolvedIds = await context.ResolvedWorkItems
            .OrderBy(item => item.WorkItemId)
            .Select(item => item.WorkItemId)
            .ToListAsync();

        Assert.IsTrue(run1Result.Success);
        Assert.IsTrue(run2Result.Success);
        Assert.IsTrue(resolutionResult.Success);
        Assert.HasCount(1, relationshipEdges, "Second snapshot run should fully replace the prior graph.");
        Assert.AreEqual(100, relationshipEdges[0].TargetWorkItemId);
        Assert.AreEqual(200, relationshipEdges[0].SourceWorkItemId);
        Assert.AreEqual(run2Result.SnapshotAsOfUtc, relationshipEdges[0].SnapshotAsOfUtc);
        CollectionAssert.AreEqual(new List<int> { 100, 200 }, resolvedIds);
    }

    private static WorkItemDto CreateWorkItemDto(
        int tfsId,
        string type,
        string title,
        DateTimeOffset retrievedAt,
        int? parentId = null)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: parentId,
            AreaPath: "\\Project",
            IterationPath: "\\Project\\Sprint 1",
            State: "New",
            RetrievedAt: retrievedAt,
            Effort: null,
            Description: null,
            ChangedDate: retrievedAt);
    }

    private static async Task SeedProfileAndPersistedWorkItemsAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

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
                    Name = "Product A",
                    BacklogRoots = [new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 }]
                }
            }
        });

        context.WorkItems.AddRange(
            CreateWorkItemEntity(100, WorkItemType.Feature, "A"),
            CreateWorkItemEntity(200, WorkItemType.Pbi, "B", parentId: 100),
            CreateWorkItemEntity(300, WorkItemType.Pbi, "C", parentId: 200));

        await context.SaveChangesAsync();
    }

    private static WorkItemEntity CreateWorkItemEntity(
        int tfsId,
        string type,
        string title,
        int? parentId = null)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            ParentTfsId = parentId,
            AreaPath = "\\Project",
            IterationPath = "\\Project\\Sprint 1",
            State = "New",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
        };
    }

    private static ServiceProvider BuildServiceProvider(string databaseName, ITfsClient tfsClient)
    {
        return new ServiceCollection()
            .AddDbContext<PoToolDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddSingleton(tfsClient)
            .BuildServiceProvider();
    }
}
