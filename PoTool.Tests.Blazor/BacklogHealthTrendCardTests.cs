using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Pages.Metrics.SubComponents;
using PoTool.Client.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for BacklogHealthTrendCard component
/// </summary>
[TestClass]
public class BacklogHealthTrendCardTests : BunitTestContext
{
    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();
        
        // Register BacklogHealthCalculationService
        Services.AddSingleton<BacklogHealthCalculationService>();
        
        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TestMethod]
    public void BacklogHealthTrendCard_RendersCorrectly()
    {
        // Arrange & Act
        var cut = RenderComponent<BacklogHealthTrendCard>(parameters => parameters
            .Add(p => p.TrendSummary, "Health improving")
            .Add(p => p.IterationCount, 5)
            .Add(p => p.TotalWorkItems, 150)
            .Add(p => p.TotalIssues, 12)
            .Add(p => p.EffortTrend, TrendDirection.Improving)
            .Add(p => p.ValidationTrend, TrendDirection.Stable)
            .Add(p => p.BlockerTrend, TrendDirection.Declining));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("Health improving", cut.Markup);
        Assert.Contains("5", cut.Markup);
        Assert.Contains("150", cut.Markup);
        Assert.Contains("12", cut.Markup);
        Assert.Contains("Effort:", cut.Markup);
        Assert.Contains("Validation:", cut.Markup);
        Assert.Contains("Blockers:", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealthTrendCard_DisplaysTrendIcons()
    {
        // Arrange & Act
        var cut = RenderComponent<BacklogHealthTrendCard>(parameters => parameters
            .Add(p => p.TrendSummary, "Test")
            .Add(p => p.IterationCount, 3)
            .Add(p => p.TotalWorkItems, 100)
            .Add(p => p.TotalIssues, 5)
            .Add(p => p.EffortTrend, TrendDirection.Improving)
            .Add(p => p.ValidationTrend, TrendDirection.Stable)
            .Add(p => p.BlockerTrend, TrendDirection.Declining));

        // Assert
        var icons = cut.FindComponents<MudBlazor.MudIcon>();
        Assert.IsGreaterThanOrEqualTo(icons.Count, 3, "Should render at least 3 trend icons");
    }

    [TestMethod]
    public void BacklogHealthTrendCard_HandlesZeroValues()
    {
        // Arrange & Act
        var cut = RenderComponent<BacklogHealthTrendCard>(parameters => parameters
            .Add(p => p.TrendSummary, "No data")
            .Add(p => p.IterationCount, 0)
            .Add(p => p.TotalWorkItems, 0)
            .Add(p => p.TotalIssues, 0)
            .Add(p => p.EffortTrend, TrendDirection.Unknown)
            .Add(p => p.ValidationTrend, TrendDirection.Unknown)
            .Add(p => p.BlockerTrend, TrendDirection.Unknown));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("No data", cut.Markup);
        Assert.Contains("0", cut.Markup);
    }
}
