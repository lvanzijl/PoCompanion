namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class TrendDeliveryAnalyticsExplorationDocumentTests
{
    [TestMethod]
    public void TrendDeliveryAnalyticsExploration_ReportExistsWithRequiredSectionsAndKeyFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "trend_delivery_analytics_exploration.md");

        Assert.IsTrue(File.Exists(reportPath), "The trend/delivery analytics exploration report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Trend / Delivery Analytics Exploration");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Inventory");
        StringAssert.Contains(report, "## Domain Families");
        StringAssert.Contains(report, "## Semantic Overlaps");
        StringAssert.Contains(report, "## Contradictions");
        StringAssert.Contains(report, "## CDC Candidates");
        StringAssert.Contains(report, "## Recommendation");

        StringAssert.Contains(report, "PoTool.Api/Services/SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs");
        StringAssert.Contains(report, "Velocity7d");
        StringAssert.Contains(report, "confidence");
        StringAssert.Contains(report, "A — ready candidate for CDC slice");
        StringAssert.Contains(report, "splitting into multiple slices");
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
