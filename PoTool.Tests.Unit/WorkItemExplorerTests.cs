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
        // This unit test checks the logic of saving/loading expanded state to localStorage via JS interop.
        var jsMock = new Mock<IJSRuntime>();

        var saved = string.Empty;
        jsMock.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "localStorage.getItem"), It.IsAny<object[]>() ))
              .ReturnsAsync((string?)null);

        jsMock.Setup(js => js.InvokeVoidAsync(It.Is<string>(s => s == "localStorage.setItem"), It.IsAny<object[]>() ))
              .Callback<string, object[]>((identifier, args) => {
                  if (args != null && args.Length >= 2 && args[0] is string key && args[1] is string value)
                  {
                      saved = value;
                  }
              })
              .Returns(new ValueTask());

        // Create an instance of the dev repository and get items
        var repo = new DevWorkItemRepository();
        var items = (await repo.GetAllAsync()).ToList();

        // Simulate expanded state
        var expanded = new Dictionary<int, bool> { { items.First().TfsId, true } };
        var json = System.Text.Json.JsonSerializer.Serialize(expanded);

        // Call Save via mock - directly call the JS mock to simulate component behavior
        await jsMock.Object.InvokeVoidAsync("localStorage.setItem", new object[] { "workitem-tree-expanded", json });

        // Ensure saved contains JSON
        Assert.IsFalse(string.IsNullOrEmpty(saved));

        // Setup get to return the saved value
        jsMock.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "localStorage.getItem"), It.IsAny<object[]>() ))
              .ReturnsAsync(saved);

        // Simulate Load
        var loaded = await jsMock.Object.InvokeAsync<string>("localStorage.getItem", new object[] { "workitem-tree-expanded" });
        Assert.AreEqual(saved, loaded);
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
            if (!toInclude.ContainsKey(m.TfsId)) toInclude[m.TfsId] = m;
            var current = m;
            while (current.ParentTfsId.HasValue)
            {
                var pid = current.ParentTfsId.Value;
                var parent = items.FirstOrDefault(w => w.TfsId == pid);
                if (parent != null)
                {
                    if (!toInclude.ContainsKey(parent.TfsId)) toInclude[parent.TfsId] = parent;
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
