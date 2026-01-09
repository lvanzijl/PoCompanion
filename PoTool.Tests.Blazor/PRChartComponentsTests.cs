using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PoTool.Client.Pages.PullRequests.SubComponents;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for PR chart components
/// </summary>
[TestClass]
public class PRChartComponentsTests : BunitTestContext
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
    public void PRStatusChart_RendersCorrectly()
    {
        // Arrange
        var statusData = new double[] { 10, 5, 2 };
        var statusLabels = new string[] { "Active", "Completed", "Abandoned" };

        // Act
        var cut = RenderComponent<PRStatusChart>(parameters => parameters
            .Add(p => p.StatusData, statusData)
            .Add(p => p.StatusLabels, statusLabels));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("PR Status Distribution", cut.Markup);
    }

    [TestMethod]
    public void PRStatusChart_DisplaysDonutChart()
    {
        // Arrange
        var statusData = new double[] { 10, 5, 2 };
        var statusLabels = new string[] { "Active", "Completed", "Abandoned" };

        // Act
        var cut = RenderComponent<PRStatusChart>(parameters => parameters
            .Add(p => p.StatusData, statusData)
            .Add(p => p.StatusLabels, statusLabels));

        // Assert
        var charts = cut.FindComponents<MudBlazor.MudChart>();
        Assert.HasCount(1, charts, "Should render one chart");
    }

    [TestMethod]
    public void PRTimeOpenChart_RendersCorrectly()
    {
        // Arrange
        var timeOpenData = new double[] { 5, 10, 8, 3 };
        var timeOpenLabels = new string[] { "0-1 days", "1-3 days", "3-7 days", ">7 days" };
        var chartOptions = new MudBlazor.ChartOptions();

        // Act
        var cut = RenderComponent<PRTimeOpenChart>(parameters => parameters
            .Add(p => p.TimeOpenData, timeOpenData)
            .Add(p => p.TimeOpenLabels, timeOpenLabels)
            .Add(p => p.ChartOptions, chartOptions));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("Time Open Distribution", cut.Markup);
    }

    [TestMethod]
    public void PRTimeOpenChart_DisplaysBarChart()
    {
        // Arrange
        var timeOpenData = new double[] { 5, 10, 8, 3 };
        var timeOpenLabels = new string[] { "0-1 days", "1-3 days", "3-7 days", ">7 days" };
        var chartOptions = new MudBlazor.ChartOptions();

        // Act
        var cut = RenderComponent<PRTimeOpenChart>(parameters => parameters
            .Add(p => p.TimeOpenData, timeOpenData)
            .Add(p => p.TimeOpenLabels, timeOpenLabels)
            .Add(p => p.ChartOptions, chartOptions));

        // Assert
        var charts = cut.FindComponents<MudBlazor.MudChart>();
        Assert.HasCount(1, charts, "Should render one chart");
    }

    [TestMethod]
    public void PRUserChart_RendersCorrectly()
    {
        // Arrange
        var userData = new double[] { 15, 12, 8, 5 };
        var userLabels = new string[] { "Alice", "Bob", "Charlie", "Diana" };
        var chartOptions = new MudBlazor.ChartOptions();

        // Act
        var cut = RenderComponent<PRUserChart>(parameters => parameters
            .Add(p => p.UserData, userData)
            .Add(p => p.UserLabels, userLabels)
            .Add(p => p.ChartOptions, chartOptions));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("PRs by User", cut.Markup);
    }

    [TestMethod]
    public void PRUserChart_DisplaysBarChart()
    {
        // Arrange
        var userData = new double[] { 15, 12, 8, 5 };
        var userLabels = new string[] { "Alice", "Bob", "Charlie", "Diana" };
        var chartOptions = new MudBlazor.ChartOptions();

        // Act
        var cut = RenderComponent<PRUserChart>(parameters => parameters
            .Add(p => p.UserData, userData)
            .Add(p => p.UserLabels, userLabels)
            .Add(p => p.ChartOptions, chartOptions));

        // Assert
        var charts = cut.FindComponents<MudBlazor.MudChart>();
        Assert.HasCount(1, charts, "Should render one chart");
    }

    [TestMethod]
    public void PRCharts_HandleEmptyData()
    {
        // Arrange
        var emptyData = Array.Empty<double>();
        var emptyLabels = Array.Empty<string>();

        // Act & Assert - Should not throw
        var statusChart = RenderComponent<PRStatusChart>(parameters => parameters
            .Add(p => p.StatusData, emptyData)
            .Add(p => p.StatusLabels, emptyLabels));
        Assert.IsNotNull(statusChart);

        var timeOpenChart = RenderComponent<PRTimeOpenChart>(parameters => parameters
            .Add(p => p.TimeOpenData, emptyData)
            .Add(p => p.TimeOpenLabels, emptyLabels));
        Assert.IsNotNull(timeOpenChart);

        var userChart = RenderComponent<PRUserChart>(parameters => parameters
            .Add(p => p.UserData, emptyData)
            .Add(p => p.UserLabels, emptyLabels));
        Assert.IsNotNull(userChart);
    }
}
