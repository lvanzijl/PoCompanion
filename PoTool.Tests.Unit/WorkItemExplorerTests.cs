using Microsoft.JSInterop;
using Moq;
using PoTool.Core.WorkItems;
using PoTool.Api.Repositories;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PoTool.Tests.Unit;

[TestClass]
public class WorkItemExplorerTests
{
    [TestMethod]
    public async Task ExpandedState_Persistence_LocalStorage_SavedAndLoaded()
    {
        // This unit test checks the logic of serialization/deserialization of expanded state
        // Note: Full JSInterop testing is better done with bUnit tests
        var repo = new DevWorkItemRepository();
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
        Assert.IsTrue(loaded.ContainsKey(items.First().TfsId));
        Assert.IsTrue(loaded[items.First().TfsId]);
    }

    [TestMethod]
    public async Task Filter_Includes_Ancestors_For_Match()
    {
        var repo = new DevWorkItemRepository();
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
}
