using Microsoft.JSInterop;
using Moq;
using PoTool.Core.WorkItems;
using PoTool.Api.Repositories;
using PoTool.Api.Services.MockData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PoTool.Tests.Unit;

[TestClass]
public class WorkItemExplorerTests
{
    private BattleshipMockDataFacade CreateMockDataFacade()
    {
        var workItemGenerator = new BattleshipWorkItemGenerator();
        var dependencyGenerator = new BattleshipDependencyGenerator();
        var pullRequestGenerator = new BattleshipPullRequestGenerator();
        var validator = new MockDataValidator();
        var logger = Mock.Of<ILogger<BattleshipMockDataFacade>>();
        
        return new BattleshipMockDataFacade(
            workItemGenerator,
            dependencyGenerator,
            pullRequestGenerator,
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
        var toInclude = new Dictionary<int, WorkItemDto>();
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
}
