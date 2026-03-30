namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ApplicationHandlerCleanupDocumentTests
{
    [TestMethod]
    public void ApplicationHandlerCleanup_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "application_handler_cleanup.md");

        Assert.IsTrue(File.Exists(reportPath), "The application handler cleanup report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Application Handler Cleanup");
        StringAssert.Contains(report, "## Handlers Scanned");
        StringAssert.Contains(report, "## Helpers Removed");
        StringAssert.Contains(report, "## Helpers Retained (UI-only)");
        StringAssert.Contains(report, "## Lines of Code Removed");
        StringAssert.Contains(report, "## Final Handler Responsibilities");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandler.cs");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "GetEffortImbalanceQueryHandler.cs");
        StringAssert.Contains(report, "GetEffortConcentrationRiskQueryHandler.cs");
        StringAssert.Contains(report, "GetSprintCapacityPlanQueryHandler.cs");
        StringAssert.Contains(report, "GetHomeProductBarMetricsQueryHandler.cs");
        StringAssert.Contains(report, "none in this pass");
        StringAssert.Contains(report, "`0` additional handler lines");
        StringAssert.Contains(report, "ISprintFactService");
        StringAssert.Contains(report, "IBacklogQualityAnalysisService");
        StringAssert.Contains(report, "IPortfolioFlowSummaryService");
        StringAssert.Contains(report, "IPortfolioDeliverySummaryService");
        StringAssert.Contains(report, "EffortDiagnosticsAnalyzer");
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
