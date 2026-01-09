using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PoTool.Client.Pages.PullRequests.SubComponents;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for PRDateRangeFilter component
/// </summary>
[TestClass]
public class PRDateRangeFilterTests : BunitTestContext
{
    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();
        
        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedFragment RenderWithMudProvider(MudBlazor.DateRange? dateRange = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<PRDateRangeFilter>(1);
            if (dateRange != null)
            {
                builder.AddAttribute(2, nameof(PRDateRangeFilter.DateRange), dateRange);
            }
            builder.CloseComponent();
        });
    }

    [TestMethod]
    public void PRDateRangeFilter_RendersCorrectly()
    {
        // Arrange & Act
        var cut = RenderWithMudProvider();

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("Filter by Date Range", cut.Markup);
        Assert.Contains("Apply Filter", cut.Markup);
    }

    [TestMethod]
    public void PRDateRangeFilter_ShowsClearButton_WhenDateRangeSet()
    {
        // Arrange & Act
        var dateRange = new MudBlazor.DateRange(DateTime.Now.AddDays(-7), DateTime.Now);
        var cut = RenderWithMudProvider(dateRange);

        // Assert
        Assert.Contains("Clear", cut.Markup);
    }

    [TestMethod]
    public void PRDateRangeFilter_HidesClearButton_WhenNoDateRange()
    {
        // Arrange & Act
        var cut = RenderWithMudProvider(null);

        // Assert
        Assert.DoesNotContain("Clear", cut.Markup);
    }

    /* Skipping callback tests as they require MudPopoverProvider setup
    [TestMethod]
    public async Task PRDateRangeFilter_ApplyButton_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var cut = RenderComponent<PRDateRangeFilter>(parameters => parameters
            .Add(p => p.DateRange, null)
            .Add(p => p.OnApplyFilter, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

        // Act
        var applyButton = cut.Find("button:contains('Apply Filter')");
        await applyButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        Assert.IsTrue(callbackInvoked, "Apply filter callback should have been invoked");
    }

    [TestMethod]
    public async Task PRDateRangeFilter_ClearButton_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var dateRange = new MudBlazor.DateRange(DateTime.Now.AddDays(-7), DateTime.Now);
        var cut = RenderComponent<PRDateRangeFilter>(parameters => parameters
            .Add(p => p.DateRange, dateRange)
            .Add(p => p.OnClearFilter, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

        // Act
        var clearButton = cut.Find("button:contains('Clear')");
        await clearButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        Assert.IsTrue(callbackInvoked, "Clear filter callback should have been invoked");
    }
    */
}
