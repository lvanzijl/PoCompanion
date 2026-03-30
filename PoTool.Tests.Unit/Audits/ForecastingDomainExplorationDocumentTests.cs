namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ForecastingDomainExplorationDocumentTests
{
    [TestMethod]
    public void ForecastingDomainExploration_ReportExistsWithRequiredSectionsAndBoundaries()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "forecasting-domain-exploration.md");

        Assert.IsTrue(File.Exists(reportPath), "The forecasting domain exploration report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Forecasting Domain Exploration");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Search scope");
        StringAssert.Contains(report, "## Forecasting inventory and classification");
        StringAssert.Contains(report, "## Slice dependency map");
        StringAssert.Contains(report, "## Duplicated logic");
        StringAssert.Contains(report, "## Contradictions and semantic drift");
        StringAssert.Contains(report, "## Recommended boundaries for a Forecasting slice");
        StringAssert.Contains(report, "## Recommended extraction order");
        StringAssert.Contains(report, "## Final recommendation");

        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Services/SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Client/Services/RoadmapAnalyticsService.cs");
        StringAssert.Contains(report, "PoTool.Client/Components/Forecast/ForecastPanel.razor");
        StringAssert.Contains(report, "PoTool.Shared/Statistics/PercentileMath.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs");
        StringAssert.Contains(report, "no production code implementing a dedicated burn-up or burn-down predictor was found");
        StringAssert.Contains(report, "Forecasting should be a separate CDC slice layered on top of DeliveryTrends");
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
