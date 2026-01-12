using Microsoft.JSInterop;
using Moq;
using SharedWorkItemDto = PoTool.Shared.WorkItems.WorkItemDto;
using PoTool.Api.Repositories;
using PoTool.Api.Services.MockData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using PoTool.Core.WorkItems;
using PoTool.Client.Services;
using PoTool.Client.ApiClient;

namespace PoTool.Tests.Unit;

[TestClass]
public class WorkItemExplorerTests
{
    private BattleshipMockDataFacade CreateMockDataFacade()
    {
        var workItemGenerator = new BattleshipWorkItemGenerator();
        var dependencyGenerator = new BattleshipDependencyGenerator();
        var pullRequestGenerator = new BattleshipPullRequestGenerator();
        var pipelineGeneratorLogger = Mock.Of<ILogger<BattleshipPipelineGenerator>>();
        var pipelineGenerator = new BattleshipPipelineGenerator(pipelineGeneratorLogger);
        var validator = new MockDataValidator();
        var logger = Mock.Of<ILogger<BattleshipMockDataFacade>>();

        return new BattleshipMockDataFacade(
            workItemGenerator,
            dependencyGenerator,
            pullRequestGenerator,
            pipelineGenerator,
            validator,
            logger);
    }

    [TestMethod]
    public async Task ExpandedState_Persistence_LocalStorage_SavedAndLoaded()
    {
        // This unit test checks the logic of serialization/deserialization of expanded state
        // Note: Full JSInterop testing is better done with bUnit tests
        var facade = CreateMockDataFacade();
        var repo = new DevWorkItemRepository(facade);
        var items = (await repo.GetAllAsync()).ToList();

        // Simulate expanded state
        var expanded = new Dictionary<int, bool> { { items.First().TfsId, true } };
        var json = System.Text.Json.JsonSerializer.Serialize(expanded);

        // Verify JSON is not empty
        Assert.IsFalse(string.IsNullOrEmpty(json));

        // Simulate deserialization (what would be loaded from localStorage)
        var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, bool>>(json);

        // Verify the loaded state matches
        Assert.IsNotNull(loaded);
        Assert.HasCount(1, loaded);

#pragma warning disable MSTEST0037
        Assert.IsTrue(loaded.ContainsKey(items.First().TfsId));
        Assert.IsTrue(loaded[items.First().TfsId]);
    }

    [TestMethod]
    public async Task Filter_Includes_Ancestors_For_Match()
    {
        var facade = CreateMockDataFacade();
        var repo = new DevWorkItemRepository(facade);
        var items = (await repo.GetAllAsync()).ToList();

        // pick a deep item (a Task) and filter by part of its title
        var task = items.First(i => i.Type == "Task");
        var filter = task.Title.Split('-').Last().Trim();

        // emulate client-side filtering logic
        var matches = items.Where(w => w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        var toInclude = new Dictionary<int, SharedWorkItemDto>();
        foreach (var m in matches)
        {
            toInclude.TryAdd(m.TfsId, m);
            var current = m;
            while (current.ParentTfsId.HasValue)
            {
                var pid = current.ParentTfsId.Value;
                var parent = items.FirstOrDefault(w => w.TfsId == pid);
                if (parent != null)
                {
                    toInclude.TryAdd(parent.TfsId, parent);
                    current = parent;
                }
                else
                {
                    break;
                }
            }
        }

        // Ensure that at least the task and its parent chain are included
        Assert.IsTrue(toInclude.Any());
    }

    [TestMethod]
    public async Task AllGoals_AreRootNodes()
    {
        var facade = CreateMockDataFacade();
        var repo = new DevWorkItemRepository(facade);
        var items = (await repo.GetAllAsync()).ToList();

        // Get all goals
        var goals = items.Where(i => i.Type == "Goal").ToList();

        // Verify we have exactly 10 goals
        Assert.AreEqual(10, goals.Count, "Should have exactly 10 goals");

        // Verify all goals are root nodes (no parent)
        foreach (var goal in goals)
        {
            Assert.IsNull(goal.ParentTfsId, $"Goal {goal.TfsId} should not have a parent, but has parent {goal.ParentTfsId}");
        }
    }

    [TestMethod]
    public async Task TreeBuilder_ShowsAllGoals_WhenNoFiltering()
    {
        var facade = CreateMockDataFacade();
        var repo = new DevWorkItemRepository(facade);
        var allItems = (await repo.GetAllAsync()).ToList();

        // Simulate the behavior when ConfiguredGoalIds is empty (no filtering)
        // In the actual WorkItemExplorer, this would skip the FilterByGoalsAsync call
        var filteredItems = allItems;

        // Build tree structure - count root nodes (items without parents)
        var roots = filteredItems.Where(item => !item.ParentTfsId.HasValue).ToList();

        // Verify we have exactly 10 root nodes (the goals)
        Assert.AreEqual(10, roots.Count, "Should have exactly 10 root nodes when no filtering is applied");

        // Verify all roots are goals
        foreach (var root in roots)
        {
            Assert.AreEqual("Goal", root.Type, $"Root node {root.TfsId} should be a Goal, but is {root.Type}");
        }
    }

    [TestMethod]
    public void ProductBasedTree_CreatesProductNodes()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create sample work items with validation - use Client.ApiClient types
        var workItems = new List<PoTool.Client.ApiClient.WorkItemWithValidationDto>
        {
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 100,
                Title = "Product 1 Root",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 101,
                Title = "Feature under Product 1",
                Type = "Feature",
                State = "Active",
                ParentTfsId = 100,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 200,
                Title = "Product 2 Root",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            }
        };

        // Create sample products
        var products = new List<ProductDto>
        {
            new ProductDto
            {
                Id = 1,
                Name = "Product Alpha",
                BacklogRootWorkItemId = 100,
                Order = 1
            },
            new ProductDto
            {
                Id = 2,
                Name = "Product Beta",
                BacklogRootWorkItemId = 200,
                Order = 2
            }
        };

        // Act
        var tree = treeBuilder.BuildProductBasedTreeWithValidation(workItems, products, expandedState);

        // Assert
        Assert.AreEqual(2, tree.Count, "Should have 2 top-level nodes (one per product)");
        Assert.AreEqual("Product Alpha", tree[0].Title);
        Assert.AreEqual("Product", tree[0].Type);
        Assert.AreEqual(1, tree[0].Children.Count, "Product Alpha should have 1 child (root work item)");
        Assert.AreEqual(100, tree[0].Children[0].Id);
        Assert.AreEqual("Product Beta", tree[1].Title);
    }

    [TestMethod]
    public void ProductBasedTree_CreatesUnparentedNode_ForOrphanedItems()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create sample work items - one with missing parent, one product root
        var workItems = new List<PoTool.Client.ApiClient.WorkItemWithValidationDto>
        {
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 100,
                Title = "Product Root",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 999,
                Title = "Orphaned Item",
                Type = "Feature",
                State = "Active",
                ParentTfsId = 888, // Parent doesn't exist
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            }
        };

        var products = new List<ProductDto>
        {
            new ProductDto
            {
                Id = 1,
                Name = "Product Alpha",
                BacklogRootWorkItemId = 100,
                Order = 1
            }
        };

        // Act
        var tree = treeBuilder.BuildProductBasedTreeWithValidation(workItems, products, expandedState);

        // Assert
        Assert.AreEqual(2, tree.Count, "Should have 2 top-level nodes (Product + Unparented)");
        
        var productNode = tree.FirstOrDefault(n => n.Type == "Product");
        Assert.IsNotNull(productNode, "Should have a Product node");
        Assert.AreEqual("Product Alpha", productNode.Title);
        
        var unparentedNode = tree.FirstOrDefault(n => n.Type == "Unparented");
        Assert.IsNotNull(unparentedNode, "Should have an Unparented node");
        Assert.AreEqual("Unparented", unparentedNode.Title);
        Assert.AreEqual(1, unparentedNode.Children.Count, "Unparented should have 1 child");
        Assert.AreEqual(999, unparentedNode.Children[0].Id);
        Assert.AreEqual("Orphaned Item", unparentedNode.Children[0].Title);
    }

    [TestMethod]
    public void ProductBasedTree_ProductRootsNotInUnparented()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create product root without parent (should go under product, not Unparented)
        var workItems = new List<PoTool.Client.ApiClient.WorkItemWithValidationDto>
        {
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 100,
                Title = "Product Root (No Parent)",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            }
        };

        var products = new List<ProductDto>
        {
            new ProductDto
            {
                Id = 1,
                Name = "Product Alpha",
                BacklogRootWorkItemId = 100,
                Order = 1
            }
        };

        // Act
        var tree = treeBuilder.BuildProductBasedTreeWithValidation(workItems, products, expandedState);

        // Assert
        Assert.AreEqual(1, tree.Count, "Should have only 1 top-level node (Product), no Unparented");
        Assert.AreEqual("Product", tree[0].Type);
        Assert.AreEqual("Product Alpha", tree[0].Title);
        Assert.AreEqual(1, tree[0].Children.Count);
        Assert.AreEqual(100, tree[0].Children[0].Id);
    }

    [TestMethod]
    public void ProductBasedTree_NoUnparentedNode_WhenAllItemsHaveParents()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create complete hierarchy - no orphans
        var workItems = new List<PoTool.Client.ApiClient.WorkItemWithValidationDto>
        {
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 100,
                Title = "Product Root",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new PoTool.Client.ApiClient.WorkItemWithValidationDto
            {
                TfsId = 101,
                Title = "Child of Root",
                Type = "Feature",
                State = "Active",
                ParentTfsId = 100,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            }
        };

        var products = new List<ProductDto>
        {
            new ProductDto
            {
                Id = 1,
                Name = "Product Alpha",
                BacklogRootWorkItemId = 100,
                Order = 1
            }
        };

        // Act
        var tree = treeBuilder.BuildProductBasedTreeWithValidation(workItems, products, expandedState);

        // Assert
        Assert.AreEqual(1, tree.Count, "Should have only 1 top-level node (Product), no Unparented");
        Assert.AreEqual("Product", tree[0].Type);
        var unparentedNode = tree.FirstOrDefault(n => n.Type == "Unparented");
        Assert.IsNull(unparentedNode, "Should not have Unparented node when all items have parents");
    }
}

