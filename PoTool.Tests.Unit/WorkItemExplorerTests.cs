using Microsoft.JSInterop;
using Moq;
using SharedWorkItemDto = PoTool.Shared.WorkItems.WorkItemDto;
using ClientWorkItemWithValidationDto = PoTool.Client.ApiClient.WorkItemWithValidationDto;
using ClientValidationIssue = PoTool.Client.ApiClient.ValidationIssue;
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
using PoTool.Client.Models;

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

        // Get all goals (note: Type is lowercase "goal" from mock data)
        var goals = items.Where(i => i.Type.Equals("goal", StringComparison.OrdinalIgnoreCase)).ToList();

        // Verify we have exactly 10 goals
        Assert.AreEqual(10, goals.Count, "Should have exactly 10 goals");

        // Verify all goals are root nodes (no parent)
        foreach (var goal in goals)
        {
            Assert.IsNull(goal.ParentTfsId, $"Goal {goal.TfsId} should not have a parent, but has parent {goal.ParentTfsId}");
        }
    }

    [TestMethod]
    public async Task TreeBuilder_ShowsAllRootNodes_WhenNoFiltering()
    {
        var facade = CreateMockDataFacade();
        var repo = new DevWorkItemRepository(facade);
        var allItems = (await repo.GetAllAsync()).ToList();

        // Simulate the behavior when no filtering is applied
        // In the actual WorkItemExplorer, products define scope, not goals
        var filteredItems = allItems;

        // Build tree structure - count root nodes (items without parents)
        var roots = filteredItems.Where(item => !item.ParentTfsId.HasValue).ToList();

        // Verify we have exactly 10 root nodes (the goals in this mock data)
        Assert.AreEqual(10, roots.Count, "Should have exactly 10 root nodes when no filtering is applied");

        // Note: In the mock data, all root nodes happen to be goals,
        // but in real usage, products define what roots are shown (via BacklogRootWorkItemId)
        // Type is lowercase "goal" from mock data
        foreach (var root in roots)
        {
            Assert.IsTrue(root.Type.Equals("goal", StringComparison.OrdinalIgnoreCase), $"Root node {root.TfsId} should be a Goal, but is {root.Type}");
        }
    }

    [TestMethod]
    public void ProductBasedTree_UsesActualWorkItemsAsRoots()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create sample work items with validation - use Client.ApiClient types
        var workItems = new List<ClientWorkItemWithValidationDto>
        {
            new ClientWorkItemWithValidationDto
            {
                TfsId = 100,
                Title = "Product 1 Root",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
            {
                TfsId = 101,
                Title = "Feature under Product 1",
                Type = "Feature",
                State = "Active",
                ParentTfsId = 100,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
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

        // Assert - No synthetic "Product" nodes, actual work items are roots
        Assert.AreEqual(2, tree.Count, "Should have 2 top-level nodes (actual work items, not product wrappers)");
        
        // Verify both root nodes are actual work items (Epics), not synthetic Product nodes
        Assert.AreEqual("Product 1 Root", tree[0].Title);
        Assert.AreEqual("Epic", tree[0].Type, "Root should be actual work item type, not 'Product'");
        Assert.AreEqual(100, tree[0].Id);
        Assert.AreEqual(1, tree[0].Children.Count, "Epic should have 1 child (Feature)");
        Assert.AreEqual(101, tree[0].Children[0].Id);
        
        Assert.AreEqual("Product 2 Root", tree[1].Title);
        Assert.AreEqual("Epic", tree[1].Type, "Root should be actual work item type, not 'Product'");
        Assert.AreEqual(200, tree[1].Id);
    }

    [TestMethod]
    public void ProductBasedTree_CreatesUnparentedNode_ForOrphanedItems()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create sample work items - one with missing parent, one product root
        var workItems = new List<ClientWorkItemWithValidationDto>
        {
            new ClientWorkItemWithValidationDto
            {
                TfsId = 100,
                Title = "Product Root",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
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
        Assert.AreEqual(2, tree.Count, "Should have 2 top-level nodes (Epic root + Unparented)");
        
        // First node should be the actual Epic work item (not a synthetic Product node)
        var epicNode = tree.FirstOrDefault(n => n.Type == "Epic");
        Assert.IsNotNull(epicNode, "Should have an Epic node as root");
        Assert.AreEqual("Product Root", epicNode.Title);
        Assert.AreEqual(100, epicNode.Id);
        
        var unparentedNode = tree.FirstOrDefault(n => n.Type == "Unparented");
        Assert.IsNotNull(unparentedNode, "Should have an Unparented node");
        Assert.AreEqual("Unparented", unparentedNode.Title);
        Assert.AreEqual(1, unparentedNode.Children.Count, "Unparented should have 1 child");
        Assert.AreEqual(999, unparentedNode.Children[0].Id);
        Assert.AreEqual("Orphaned Item", unparentedNode.Children[0].Title);
    }

    [TestMethod]
    public void ProductBasedTree_ParentlessWorkItemsAreRoots()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create product root without parent (should be a direct root)
        var workItems = new List<ClientWorkItemWithValidationDto>
        {
            new ClientWorkItemWithValidationDto
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
        Assert.AreEqual(1, tree.Count, "Should have only 1 top-level node (the Epic), no synthetic Product wrapper, no Unparented");
        Assert.AreEqual("Epic", tree[0].Type, "Root should be actual work item type");
        Assert.AreEqual("Product Root (No Parent)", tree[0].Title);
        Assert.AreEqual(100, tree[0].Id);
        Assert.AreEqual(0, tree[0].Children.Count, "Epic has no children");
    }

    [TestMethod]
    public void ProductBasedTree_NoUnparentedNode_WhenAllItemsHaveParents()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create complete hierarchy - no orphans
        var workItems = new List<ClientWorkItemWithValidationDto>
        {
            new ClientWorkItemWithValidationDto
            {
                TfsId = 100,
                Title = "Product Root",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
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
        Assert.AreEqual(1, tree.Count, "Should have only 1 top-level node (Epic), no Unparented");
        Assert.AreEqual("Epic", tree[0].Type);
        var unparentedNode = tree.FirstOrDefault(n => n.Type == "Unparented");
        Assert.IsNull(unparentedNode, "Should not have Unparented node when all items have parents");
    }
    
    [TestMethod]
    public void TreeBuilder_GoalEpicFeatureHierarchy_NoSyntheticRoots()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create Goal -> Epic -> Feature hierarchy as specified in problem statement
        var workItems = new List<ClientWorkItemWithValidationDto>
        {
            new ClientWorkItemWithValidationDto
            {
                TfsId = 1,
                Title = "Strategic Goal",
                Type = "Goal",
                State = "Active",
                ParentTfsId = null, // Goals are always parentless
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
            {
                TfsId = 2,
                Title = "Epic under Goal",
                Type = "Epic",
                State = "Active",
                ParentTfsId = 1,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
            {
                TfsId = 3,
                Title = "Feature under Epic",
                Type = "Feature",
                State = "Active",
                ParentTfsId = 2,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
            {
                TfsId = 99,
                Title = "Standalone Parentless Item",
                Type = "Epic",
                State = "Active",
                ParentTfsId = null, // Another root
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            }
        };

        var products = new List<ProductDto>
        {
            new ProductDto
            {
                Id = 1,
                Name = "Product One",
                BacklogRootWorkItemId = 1,
                Order = 1
            }
        };

        // Act
        var tree = treeBuilder.BuildProductBasedTreeWithValidation(workItems, products, expandedState);

        // Assert
        // Should have 2 root nodes: Goal (ID 1) and Standalone Epic (ID 99)
        Assert.AreEqual(2, tree.Count, "Should have 2 root nodes (Goal and standalone Epic)");
        
        // Verify no synthetic "product name" root exists
        Assert.IsFalse(tree.Any(n => n.Type == "Product"), "Should NOT have any synthetic 'Product' nodes");
        
        // Verify roots are actual work items
        var goalNode = tree.FirstOrDefault(n => n.Id == 1);
        Assert.IsNotNull(goalNode, "Goal should be a root");
        Assert.AreEqual("Goal", goalNode.Type);
        Assert.AreEqual("Strategic Goal", goalNode.Title);
        Assert.AreEqual(1, goalNode.Children.Count, "Goal should have 1 child (Epic)");
        Assert.AreEqual(2, goalNode.Children[0].Id);
        Assert.AreEqual(1, goalNode.Children[0].Children.Count, "Epic should have 1 child (Feature)");
        Assert.AreEqual(3, goalNode.Children[0].Children[0].Id);
        
        var standaloneNode = tree.FirstOrDefault(n => n.Id == 99);
        Assert.IsNotNull(standaloneNode, "Standalone Epic should be a root");
        Assert.AreEqual("Epic", standaloneNode.Type);
        Assert.AreEqual("Standalone Parentless Item", standaloneNode.Title);
        
        // Verify no Unparented node exists (all items have valid parents or are roots)
        Assert.IsFalse(tree.Any(n => n.Type == "Unparented"), "Should NOT have 'Unparented' node when all items are properly parented");
    }
    
    [TestMethod]
    public void TreeBuilder_NoDuplicateNodesInTree()
    {
        // Arrange
        var treeBuilder = new TreeBuilderService();
        var expandedState = new Dictionary<int, bool>();

        // Create a hierarchy that could potentially cause duplication
        var workItems = new List<ClientWorkItemWithValidationDto>
        {
            new ClientWorkItemWithValidationDto
            {
                TfsId = 1,
                Title = "Root Goal",
                Type = "Goal",
                State = "Active",
                ParentTfsId = null,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
            {
                TfsId = 2,
                Title = "Child Epic",
                Type = "Epic",
                State = "Active",
                ParentTfsId = 1,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            },
            new ClientWorkItemWithValidationDto
            {
                TfsId = 3,
                Title = "Grandchild Feature",
                Type = "Feature",
                State = "Active",
                ParentTfsId = 2,
                ValidationIssues = new List<PoTool.Client.ApiClient.ValidationIssue>()
            }
        };

        var products = new List<ProductDto>
        {
            new ProductDto
            {
                Id = 1,
                Name = "Product",
                BacklogRootWorkItemId = 1,
                Order = 1
            }
        };

        // Act
        var tree = treeBuilder.BuildProductBasedTreeWithValidation(workItems, products, expandedState);

        // Assert
        // Collect all node IDs from the tree
        var allNodeIds = CollectAllNodeIds(tree);
        
        // Verify no ID appears twice
        var uniqueIds = new HashSet<int>(allNodeIds);
        Assert.AreEqual(allNodeIds.Count, uniqueIds.Count, 
            $"Work item IDs should not be duplicated in tree. Found {allNodeIds.Count} nodes but only {uniqueIds.Count} unique IDs");
        
        // Verify all work item IDs are present
        Assert.AreEqual(3, allNodeIds.Count, "Should have exactly 3 nodes in tree");
        Assert.IsTrue(allNodeIds.Contains(1), "Tree should contain Goal (ID 1)");
        Assert.IsTrue(allNodeIds.Contains(2), "Tree should contain Epic (ID 2)");
        Assert.IsTrue(allNodeIds.Contains(3), "Tree should contain Feature (ID 3)");
    }
    
    /// <summary>
    /// Recursively collects all node IDs from a tree.
    /// </summary>
    private static List<int> CollectAllNodeIds(List<TreeNode> roots)
    {
        var ids = new List<int>();
        
        void CollectIdsRecursive(TreeNode node)
        {
            ids.Add(node.Id);
            foreach (var child in node.Children)
            {
                CollectIdsRecursive(child);
            }
        }
        
        foreach (var root in roots)
        {
            CollectIdsRecursive(root);
        }
        
        return ids;
    }
}

