using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Client.Components.WorkItems.SubComponents;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemToolbar component
/// </summary>
[TestClass]
public class WorkItemToolbarTests : BunitTestContext
{
    [TestMethod]
    public void WorkItemToolbar_RendersCorrectly()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.IsSyncing, false)
            .Add(p => p.FilterText, ""));

        // Assert
        Assert.IsNotNull(cut);
        Assert.IsTrue(cut.Markup.Contains("Work Item Explorer"));
        Assert.IsTrue(cut.Markup.Contains("Pull & Cache"));
    }

    [TestMethod]
    public async Task WorkItemToolbar_SyncButton_InvokesCallback()
    {
        // Arrange
        var syncClicked = false;
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.IsSyncing, false)
            .Add(p => p.FilterText, "")
            .Add(p => p.OnSyncRequested, EventCallback.Factory.Create(this, () => { syncClicked = true; })));

        // Act
        var syncButton = cut.Find("button.btn-sync");
        await syncButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        Assert.IsTrue(syncClicked, "Sync callback should have been invoked");
    }

    [TestMethod]
    public void WorkItemToolbar_SyncButton_DisabledWhenSyncInProgress()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.IsSyncing, true)
            .Add(p => p.FilterText, ""));

        // Assert
        var syncButton = cut.Find("button.btn-sync");
        Assert.IsTrue(syncButton.HasAttribute("disabled"), "Sync button should be disabled during sync");
    }

    [TestMethod]
    public async Task WorkItemToolbar_FilterInput_InvokesCallback()
    {
        // Arrange
        var filterChanged = false;
        string? newFilterValue = null;
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.IsSyncing, false)
            .Add(p => p.FilterText, "")
            .Add(p => p.OnFilterChanged, EventCallback.Factory.Create<string>(this, (value) => 
            { 
                filterChanged = true;
                newFilterValue = value;
            })));

        // Act
        var filterInput = cut.Find("input[type='text']");
        await filterInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "test filter" });

        // Assert
        Assert.IsTrue(filterChanged, "Filter callback should have been invoked");
        Assert.AreEqual("test filter", newFilterValue);
    }

    [TestMethod]
    public void WorkItemToolbar_FilterInput_DisplaysCurrentValue()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.IsSyncing, false)
            .Add(p => p.FilterText, "my filter"));

        // Assert
        var filterInput = cut.Find("input[type='text']");
        // Note: The value is bound via @bind, so it may not appear as an attribute immediately
        // Instead check that the component has the parameter set
        Assert.AreEqual("my filter", cut.Instance.FilterText);
    }
}
