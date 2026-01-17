using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.Components.WorkItems.SubComponents;
using PoTool.Client.Services;
using PoTool.Shared.Contracts;
using PoTool.Client.ApiClient;
using Moq;
using System.Linq;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemToolbar component with MudBlazor infrastructure
/// Tests wrap components with MudPopoverProvider to satisfy MudTooltip requirements
/// </summary>
[TestClass]
public class WorkItemToolbarTests : BunitTestContext
{
    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();

        // Register required services
        Services.AddSingleton<ExportService>();
        Services.AddSingleton<ReportService>();

        // Mock interface services
        var mockClipboardService = new Mock<IClipboardService>();
        mockClipboardService.Setup(x => x.CopyToClipboardAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        Services.AddSingleton(mockClipboardService.Object);

        // Mock TfsConfigService dependencies
        var mockApiClient = new Mock<IClient>();
        var mockSecureStorage = new Mock<ISecureStorageService>();
        Services.AddSingleton(mockApiClient.Object);
        Services.AddSingleton(mockSecureStorage.Object);
        Services.AddSingleton<TfsConfigService>();

        // Configure JSInterop in Loose mode to allow any JS calls without explicit setup
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedFragment RenderWithMudProvider(RenderFragment childContent)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.AddContent(1, childContent);
        });
    }

    [TestMethod]
    public void WorkItemToolbar_RendersCorrectly()
    {
        // Arrange & Act
        var cut = RenderWithMudProvider(builder =>
        {
            builder.OpenComponent<WorkItemToolbar>(0);
            builder.AddAttribute(1, "FilterText", "");
            builder.AddAttribute(2, "SelectedCount", 0);
            builder.AddAttribute(3, "ValidationFilters", new List<PoTool.Client.Models.ValidationFilter>());
            builder.CloseComponent();
        });

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("Work Item Explorer", cut.Markup);
        Assert.Contains("Clear Tree State", cut.Markup);
    }

    [TestMethod]
    public void WorkItemToolbar_HasFilterInput()
    {
        // Arrange & Act
        var cut = RenderWithMudProvider(builder =>
        {
            builder.OpenComponent<WorkItemToolbar>(0);
            builder.AddAttribute(1, "FilterText", "");
            builder.AddAttribute(2, "SelectedCount", 0);
            builder.AddAttribute(3, "ValidationFilters", new List<PoTool.Client.Models.ValidationFilter>());
            builder.CloseComponent();
        });

        // Assert - Verify filter input exists
        var filterInput = cut.Find("input[type='text']");
        Assert.IsNotNull(filterInput, "Should find filter input");
    }

    [TestMethod]
    public void WorkItemToolbar_ShowsSelectionInfo_WhenItemsSelected()
    {
        // Arrange & Act
        var cut = RenderWithMudProvider(builder =>
        {
            builder.OpenComponent<WorkItemToolbar>(0);
            builder.AddAttribute(1, "FilterText", "");
            builder.AddAttribute(2, "SelectedCount", 5);
            builder.AddAttribute(3, "ValidationFilters", new List<PoTool.Client.Models.ValidationFilter>());
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("5 items selected", cut.Markup);
    }

    [TestMethod]
    public async Task WorkItemToolbar_FilterInput_InvokesCallback()
    {
        // Arrange
        var filterChanged = false;
        string? newFilterValue = null;
        var cut = RenderWithMudProvider(builder =>
        {
            builder.OpenComponent<WorkItemToolbar>(0);
            builder.AddAttribute(1, "FilterText", "");
            builder.AddAttribute(2, "SelectedCount", 0);
            builder.AddAttribute(3, "ValidationFilters", new List<PoTool.Client.Models.ValidationFilter>());
            builder.AddAttribute(4, "OnFilterChanged", EventCallback.Factory.Create<string>(this, (value) =>
            {
                filterChanged = true;
                newFilterValue = value;
            }));
            builder.CloseComponent();
        });

        // Act
        var filterInput = cut.Find("input[type='text']");
        await filterInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "test filter" });
        // Trigger keyup event which actually invokes the callback
        await filterInput.KeyUpAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs());

        // Wait for debounce timer (300ms + buffer)
        await Task.Delay(400);

        // Assert
        Assert.IsTrue(filterChanged, "Filter callback should have been invoked");
        Assert.AreEqual("test filter", newFilterValue);
    }

    [TestMethod]
    public void WorkItemToolbar_FilterInput_DisplaysCurrentValue()
    {
        // Arrange & Act
        var cut = RenderWithMudProvider(builder =>
        {
            builder.OpenComponent<WorkItemToolbar>(0);
            builder.AddAttribute(1, "FilterText", "my filter");
            builder.AddAttribute(2, "SelectedCount", 0);
            builder.AddAttribute(3, "ValidationFilters", new List<PoTool.Client.Models.ValidationFilter>());
            builder.CloseComponent();
        });

        // Assert - Find the WorkItemToolbar instance
        var toolbar = cut.FindComponent<WorkItemToolbar>();
        Assert.AreEqual("my filter", toolbar.Instance.FilterText);
    }
}
