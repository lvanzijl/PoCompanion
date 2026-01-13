using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Shared.WorkItems;
using System.Linq;
using System.Threading.Tasks;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests to verify that work item hierarchy retrieval correctly fetches DESCENDANTS (children)
/// starting from a root work item ID, not ANCESTORS (parents).
/// </summary>
[TestClass]
public class WorkItemHierarchyRetrievalTests
{
    /// <summary>
    /// Verifies that starting from a root work item (Goal), the retrieval fetches all descendants
    /// (Objectives, Epics, Features, PBIs, Tasks) and NOT ancestors.
    /// 
    /// Uses the Battleship mock data which has a known hierarchy:
    ///   Goal → Objective → Epic → Feature → PBI/Bug → Task
    /// 
    /// Expected: Querying from a Goal ID should return the Goal and all its descendants
    /// NOT expected: Should NOT fetch any items where the Goal is a child (no ancestors should exist for a Goal anyway)
    /// </summary>
    [TestMethod]
    public async Task MockTfsClient_GetWorkItemsByRootIdsAsync_FetchesDescendantsFromGoal()
    {
        // Arrange - Create mock TFS client with all dependencies
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<BattleshipPipelineGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        services.AddSingleton<MockTfsClient>();
        
        var provider = services.BuildServiceProvider();
        var mockTfsClient = provider.GetRequiredService<MockTfsClient>();
        var mockDataFacade = provider.GetRequiredService<BattleshipMockDataFacade>();
        
        // Get all mock data to find a Goal ID
        var allItems = mockDataFacade.GetMockHierarchy();
        var firstGoal = allItems.First(wi => wi.Type == "goal"); // Note: "goal" is lowercase per WorkItemType.Goal
        var rootId = firstGoal.TfsId;
        
        // Count expected descendants by walking the hierarchy manually
        var expectedDescendantCount = CountDescendants(allItems, rootId);

        // Act
        var result = await mockTfsClient.GetWorkItemsByRootIdsAsync(
            new[] { rootId },
            since: null,
            progressCallback: null,
            cancellationToken: default);

        var resultIds = result.Select(wi => wi.TfsId).ToHashSet();

        // Assert - Should contain root
        CollectionAssert.Contains(resultIds.ToList(), rootId, $"Should contain root Goal {rootId}");
        
        // Assert - Should have root + all descendants
        var expectedTotal = 1 + expectedDescendantCount;
        Assert.HasCount(expectedTotal, resultIds, 
            $"Should have Goal + {expectedDescendantCount} descendants = {expectedTotal} total items");
        
        // Assert - All returned items should be the root or have the root as an ancestor
        foreach (var item in result)
        {
            var isRootOrDescendant = (item.TfsId == rootId) || IsDescendantOf(allItems.ToList(), item.TfsId, rootId);
            Assert.IsTrue(isRootOrDescendant, 
                $"Item {item.TfsId} ({item.Type}: {item.Title}) should be root or descendant of root {rootId}");
        }
        
        // Assert - Should NOT contain any ancestors of the root (Goals have no parents, so this is a sanity check)
        var itemsWithRootAsParent = allItems.Where(wi => wi.ParentTfsId == rootId).ToList();
        Assert.IsTrue(itemsWithRootAsParent.Any(), "Root should have at least some children in mock data");
    }

    /// <summary>
    /// Verifies descendant fetching works correctly when starting from an Objective (mid-level item).
    /// This tests that we don't accidentally fetch upwards to the parent Goal.
    /// </summary>
    [TestMethod]
    public async Task MockTfsClient_GetWorkItemsByRootIdsAsync_FetchesDescendantsFromObjective_NotParentGoal()
    {
        // Arrange - Create mock TFS client with all dependencies
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<BattleshipPipelineGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        services.AddSingleton<MockTfsClient>();
        
        var provider = services.BuildServiceProvider();
        var mockTfsClient = provider.GetRequiredService<MockTfsClient>();
        var mockDataFacade = provider.GetRequiredService<BattleshipMockDataFacade>();
        
        var allItems = mockDataFacade.GetMockHierarchy();
        var firstObjective = allItems.First(wi => wi.Type == "Objective");
        var objectiveId = firstObjective.TfsId;
        var parentGoalId = firstObjective.ParentTfsId;
        
        Assert.IsNotNull(parentGoalId, "Objective should have a parent Goal");

        // Act
        var result = await mockTfsClient.GetWorkItemsByRootIdsAsync(
            new[] { objectiveId },
            since: null,
            progressCallback: null,
            cancellationToken: default);

        var resultIds = result.Select(wi => wi.TfsId).ToHashSet();

        // Assert - Should contain the Objective
        CollectionAssert.Contains(resultIds.ToList(), objectiveId, "Should contain root Objective");
        
        // Assert - Should NOT contain the parent Goal (ancestor)
        CollectionAssert.DoesNotContain(resultIds.ToList(), parentGoalId!.Value, 
            $"Should NOT contain parent Goal {parentGoalId} when starting from Objective {objectiveId}");
        
        // Assert - Should contain children (Epics)
        var childEpics = allItems.Where(wi => wi.ParentTfsId == objectiveId).ToList();
        Assert.IsTrue(childEpics.Any(), "Objective should have child Epics");
        foreach (var epic in childEpics)
        {
            CollectionAssert.Contains(resultIds.ToList(), epic.TfsId, 
                $"Should contain child Epic {epic.TfsId}");
        }
    }

    /// <summary>
    /// Counts how many descendants a work item has (recursively).
    /// </summary>
    private static int CountDescendants(System.Collections.Generic.List<WorkItemDto> allItems, int parentId)
    {
        var children = allItems.Where(wi => wi.ParentTfsId == parentId).ToList();
        var count = children.Count;
        foreach (var child in children)
        {
            count += CountDescendants(allItems, child.TfsId);
        }
        return count;
    }

    /// <summary>
    /// Checks if targetId is a descendant of ancestorId.
    /// </summary>
    private static bool IsDescendantOf(System.Collections.Generic.List<WorkItemDto> allItems, int targetId, int ancestorId)
    {
        var current = allItems.FirstOrDefault(wi => wi.TfsId == targetId);
        while (current != null && current.ParentTfsId.HasValue)
        {
            if (current.ParentTfsId.Value == ancestorId)
                return true;
            current = allItems.FirstOrDefault(wi => wi.TfsId == current.ParentTfsId.Value);
        }
        return false;
    }

    /// <summary>
    /// CRITICAL TEST: Verifies that incremental sync (with 'since' parameter) does NOT filter graph discovery.
    /// This is the core requirement: "Incremental sync must NEVER affect graph discovery."
    /// 
    /// Test scenario:
    /// 1. Query a hierarchy with since=future date (no items changed since that date)
    /// 2. Verify that ALL descendants are still discovered
    /// 3. This proves that discovery phase ignores the 'since' parameter
    /// </summary>
    [TestMethod]
    public async Task MockTfsClient_GetWorkItemsByRootIdsAsync_IncrementalSync_StillDiscoversUnchangedDescendants()
    {
        // Arrange - Create mock TFS client with all dependencies
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<BattleshipPipelineGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        services.AddSingleton<MockTfsClient>();
        
        var provider = services.BuildServiceProvider();
        var mockTfsClient = provider.GetRequiredService<MockTfsClient>();
        var mockDataFacade = provider.GetRequiredService<BattleshipMockDataFacade>();
        
        // Get all mock data to find a Goal ID
        var allItems = mockDataFacade.GetMockHierarchy();
        var firstGoal = allItems.First(wi => wi.Type == "goal");
        var rootId = firstGoal.TfsId;
        
        // Count expected descendants
        var expectedDescendantCount = CountDescendants(allItems, rootId);
        var expectedTotal = 1 + expectedDescendantCount; // root + descendants

        // Act - Query with a 'since' date in the future
        // This simulates incremental sync where no items have changed
        var futureDate = System.DateTimeOffset.UtcNow.AddYears(10);
        var resultWithSince = await mockTfsClient.GetWorkItemsByRootIdsAsync(
            new[] { rootId },
            since: futureDate,
            progressCallback: null,
            cancellationToken: default);

        var resultIdsWithSince = resultWithSince.Select(wi => wi.TfsId).ToHashSet();

        // Act - Query without 'since' for comparison
        var resultWithoutSince = await mockTfsClient.GetWorkItemsByRootIdsAsync(
            new[] { rootId },
            since: null,
            progressCallback: null,
            cancellationToken: default);

        var resultIdsWithoutSince = resultWithoutSince.Select(wi => wi.TfsId).ToHashSet();

        // Assert - CRITICAL: Both queries should return the exact same set of IDs
        // This proves that 'since' parameter does NOT affect graph discovery
        Assert.HasCount(resultIdsWithoutSince.Count, resultIdsWithSince,
            "Incremental sync (with 'since') must discover the same number of work items as full sync");

        CollectionAssert.AreEquivalent(resultIdsWithoutSince.ToList(), resultIdsWithSince.ToList(),
            "Incremental sync (with 'since') must discover the exact same work items as full sync. " +
            "The 'since' parameter should ONLY affect refresh logic, NOT discovery.");

        // Assert - Both should have the complete hierarchy
        Assert.HasCount(expectedTotal, resultIdsWithSince,
            $"Should discover all {expectedTotal} items (root + {expectedDescendantCount} descendants) even with future 'since' date");
    }
}

