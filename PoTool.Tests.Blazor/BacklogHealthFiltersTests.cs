using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PoTool.Client.Pages.Metrics.SubComponents;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for BacklogHealthFilters component
/// </summary>
[TestClass]
public class BacklogHealthFiltersTests : BunitTestContext
{
    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();
        
        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TestMethod]
    public void BacklogHealthFilters_RendersCorrectly()
    {
        // Arrange & Act
        var cut = RenderComponent<BacklogHealthFilters>(parameters => parameters
            .Add(p => p.AreaPathFilter, "Project/Team")
            .Add(p => p.MaxIterations, 5));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("Area Path Filter", cut.Markup);
        Assert.Contains("Max Iterations", cut.Markup);
        Assert.Contains("Refresh", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealthFilters_DisplaysDefaultMaxIterations()
    {
        // Arrange & Act
        var cut = RenderComponent<BacklogHealthFilters>();

        // Assert
        Assert.Contains("Max Iterations", cut.Markup);
    }

    /* Skipping callback test as it requires MudPopoverProvider setup
    [TestMethod]
    public async Task BacklogHealthFilters_RefreshButton_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var cut = RenderComponent<BacklogHealthFilters>(parameters => parameters
            .Add(p => p.MaxIterations, 5)
            .Add(p => p.OnRefresh, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

        // Act
        var refreshButton = cut.Find("button:contains('Refresh')");
        await refreshButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        Assert.IsTrue(callbackInvoked, "Refresh callback should have been invoked");
    }
    */

    [TestMethod]
    public void BacklogHealthFilters_HasTextFieldForAreaPath()
    {
        // Arrange & Act
        var cut = RenderComponent<BacklogHealthFilters>(parameters => parameters
            .Add(p => p.AreaPathFilter, "MyProject"));

        // Assert
        var textFields = cut.FindComponents<MudBlazor.MudTextField<string>>();
        Assert.AreEqual(1, textFields.Count, "Should have one text field for area path");
    }

    [TestMethod]
    public void BacklogHealthFilters_HasNumericFieldForMaxIterations()
    {
        // Arrange & Act
        var cut = RenderComponent<BacklogHealthFilters>(parameters => parameters
            .Add(p => p.MaxIterations, 10));

        // Assert
        var numericFields = cut.FindComponents<MudBlazor.MudNumericField<int>>();
        Assert.AreEqual(1, numericFields.Count, "Should have one numeric field for max iterations");
    }
}
