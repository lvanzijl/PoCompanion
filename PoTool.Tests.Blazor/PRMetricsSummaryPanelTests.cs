using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PoTool.Client.Pages.PullRequests.SubComponents;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for PRMetricsSummaryPanel component
/// </summary>
[TestClass]
public class PRMetricsSummaryPanelTests : BunitTestContext
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
    public void PRMetricsSummaryPanel_RendersCorrectly()
    {
        // Arrange & Act
        var cut = RenderComponent<PRMetricsSummaryPanel>(parameters => parameters
            .Add(p => p.TotalPRs, "25")
            .Add(p => p.AvgTimeOpen, "2.5d")
            .Add(p => p.AvgIterations, "3.2")
            .Add(p => p.AvgFiles, "15"));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("Total PRs", cut.Markup);
        Assert.Contains("25", cut.Markup);
        Assert.Contains("Avg Time Open", cut.Markup);
        Assert.Contains("2.5d", cut.Markup);
        Assert.Contains("Avg Iterations", cut.Markup);
        Assert.Contains("3.2", cut.Markup);
        Assert.Contains("Avg Files/PR", cut.Markup);
        Assert.Contains("15", cut.Markup);
    }

    [TestMethod]
    public void PRMetricsSummaryPanel_DisplaysZeroValues()
    {
        // Arrange & Act
        var cut = RenderComponent<PRMetricsSummaryPanel>(parameters => parameters
            .Add(p => p.TotalPRs, "0")
            .Add(p => p.AvgTimeOpen, "0d")
            .Add(p => p.AvgIterations, "0")
            .Add(p => p.AvgFiles, "0"));

        // Assert
        Assert.Contains("0", cut.Markup);
        Assert.Contains("0d", cut.Markup);
    }

    [TestMethod]
    public void PRMetricsSummaryPanel_UsesMetricSummaryCards()
    {
        // Arrange & Act
        var cut = RenderComponent<PRMetricsSummaryPanel>(parameters => parameters
            .Add(p => p.TotalPRs, "10")
            .Add(p => p.AvgTimeOpen, "1.5d")
            .Add(p => p.AvgIterations, "2.0")
            .Add(p => p.AvgFiles, "12"));

        // Assert - Should render 4 MetricSummaryCard components
        var metricCards = cut.FindComponents<Client.Components.Common.MetricSummaryCard>();
        Assert.HasCount(4, metricCards, "Should render 4 metric cards");
    }
}
