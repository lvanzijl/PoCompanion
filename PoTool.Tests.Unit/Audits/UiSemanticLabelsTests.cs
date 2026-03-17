namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class UiSemanticLabelsTests
{
    [TestMethod]
    public void StoryPointSurfaces_UseExplicitStoryPointLabels()
    {
        var repositoryRoot = GetRepositoryRoot();

        var forecastPanel = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Components", "Forecast", "ForecastPanel.razor"));
        StringAssert.Contains(forecastPanel, "Total Story Points");
        StringAssert.Contains(forecastPanel, "Delivered Story Points");
        StringAssert.Contains(forecastPanel, "Remaining Story Points");
        StringAssert.Contains(forecastPanel, "_forecastData.TotalStoryPoints");
        StringAssert.Contains(forecastPanel, "_forecastData.DeliveredStoryPoints");
        StringAssert.Contains(forecastPanel, "_forecastData.RemainingStoryPoints");
        Assert.DoesNotContain(forecastPanel, "_forecastData.TotalEffort");
        Assert.DoesNotContain(forecastPanel, "_forecastData.CompletedEffort");
        Assert.DoesNotContain(forecastPanel, "_forecastData.RemainingEffort");

        var productRoadmaps = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "ProductRoadmaps.razor"));
        StringAssert.Contains(productRoadmaps, "Delivered Story Points:");
        StringAssert.Contains(productRoadmaps, "Remaining Story Points:");
        StringAssert.Contains(productRoadmaps, "epic.TotalStoryPoints");
        StringAssert.Contains(productRoadmaps, "epic.DeliveredStoryPoints");
        StringAssert.Contains(productRoadmaps, "epic.RemainingStoryPoints");
        Assert.DoesNotContain(productRoadmaps, "epic.TotalEffort");
        Assert.DoesNotContain(productRoadmaps, "epic.DeliveredEffort");
        Assert.DoesNotContain(productRoadmaps, "epic.RemainingEffort");

        var sprintExecution = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "SprintExecution.razor"));
        StringAssert.Contains(sprintExecution, "Committed Story Points");
        StringAssert.Contains(sprintExecution, "Added Story Points");
        StringAssert.Contains(sprintExecution, "Delivered Story Points");
        StringAssert.Contains(sprintExecution, "Spillover Story Points");
        StringAssert.Contains(sprintExecution, "_data.Summary.CommittedSP");
        StringAssert.Contains(sprintExecution, "_data.Summary.AddedSP");
        StringAssert.Contains(sprintExecution, "_data.Summary.DeliveredSP");
        StringAssert.Contains(sprintExecution, "_data.Summary.SpilloverSP");

        var sprintTrend = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "SprintTrend.razor"));
        StringAssert.Contains(sprintTrend, "Delivered Story Points");
        StringAssert.Contains(sprintTrend, "Story point distribution by product");
        StringAssert.Contains(sprintTrend, "Story Points</th>");

        var portfolioProgress = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "PortfolioProgressPage.razor"));
        StringAssert.Contains(portfolioProgress, "Portfolio Flow Trend");
        StringAssert.Contains(portfolioProgress, "Portfolio Stock Trend");
        StringAssert.Contains(portfolioProgress, "Remaining Scope Ratio");
        StringAssert.Contains(portfolioProgress, "Story points delivered per sprint");
        StringAssert.Contains(portfolioProgress, "Portfolio Stock (SP)");
        StringAssert.Contains(portfolioProgress, "Inflow");
        Assert.DoesNotContain(portfolioProgress, "Remaining Effort Ratio", "Portfolio flow should use remaining scope semantics.");
    }

    [TestMethod]
    public void EffortHourSurfaces_DoNotUsePtsSuffixes()
    {
        var repositoryRoot = GetRepositoryRoot();

        var deliveryTrends = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "DeliveryTrends.razor"));
        StringAssert.Contains(deliveryTrends, "Story Point Delivery Trend");
        StringAssert.Contains(deliveryTrends, "Completed Effort (hours)");
        StringAssert.Contains(deliveryTrends, "Planned Effort (hours)");
        StringAssert.Contains(deliveryTrends, "TotalCompletedPbiStoryPoints");

        var portfolioDelivery = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "PortfolioDelivery.razor"));
        StringAssert.Contains(portfolioDelivery, "Delivered Effort (hours)");
        StringAssert.Contains(portfolioDelivery, "Product Contribution (Hours)");
        StringAssert.Contains(portfolioDelivery, "Feature Contribution (Story Points)");
        Assert.DoesNotContain(portfolioDelivery, " pts", "Portfolio delivery should not show misleading pts suffixes for effort-hour values.");

        var sprintTrend = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "SprintTrend.razor"));
        StringAssert.Contains(sprintTrend, "Δ Effort (hours)");
        Assert.DoesNotContain(sprintTrend, "Δ Effort (pts)", "Sprint trend effort deltas should use hours.");
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
