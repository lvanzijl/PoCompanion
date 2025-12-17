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
            .Add(p => p.SyncInProgress, false)
            .Add(p => p.FilterText, ""));

        // Assert
        Assert.IsNotNull(cut);
        Assert.IsTrue(cut.Markup.Contains("Work Items"));
        Assert.IsTrue(cut.Markup.Contains("Sync"));
    }

    [TestMethod]
    public void WorkItemToolbar_SyncButton_InvokesCallback()
    {
        // Arrange
        var syncClicked = false;
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.SyncInProgress, false)
            .Add(p => p.FilterText, "")
            .Add(p => p.OnSync, () => { syncClicked = true; }));

        // Act
        var syncButton = cut.Find("button.mud-button-filled-primary");
        syncButton.Click();

        // Assert
        Assert.IsTrue(syncClicked, "Sync callback should have been invoked");
    }

    [TestMethod]
    public void WorkItemToolbar_SyncButton_DisabledWhenSyncInProgress()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.SyncInProgress, true)
            .Add(p => p.FilterText, ""));

        // Assert
        var syncButton = cut.Find("button.mud-button-filled-primary");
        Assert.IsTrue(syncButton.HasAttribute("disabled"), "Sync button should be disabled during sync");
    }

    [TestMethod]
    public void WorkItemToolbar_FilterInput_InvokesCallback()
    {
        // Arrange
        var filterChanged = false;
        string? newFilterValue = null;
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.SyncInProgress, false)
            .Add(p => p.FilterText, "")
            .Add(p => p.OnFilterChanged, (value) => 
            { 
                filterChanged = true;
                newFilterValue = value;
            }));

        // Act
        var filterInput = cut.Find("input.mud-input-slot");
        filterInput.Change("test filter");

        // Assert
        Assert.IsTrue(filterChanged, "Filter callback should have been invoked");
        Assert.AreEqual("test filter", newFilterValue);
    }

    [TestMethod]
    public void WorkItemToolbar_FilterInput_DisplaysCurrentValue()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemToolbar>(parameters => parameters
            .Add(p => p.SyncInProgress, false)
            .Add(p => p.FilterText, "my filter"));

        // Assert
        var filterInput = cut.Find("input.mud-input-slot");
        Assert.AreEqual("my filter", filterInput.GetAttribute("value"));
    }
}
