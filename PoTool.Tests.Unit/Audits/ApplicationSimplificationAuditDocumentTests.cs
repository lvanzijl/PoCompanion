namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class ApplicationSimplificationAuditDocumentTests
{
    [TestMethod]
    public void ApplicationSimplificationAudit_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "application-simplification-audit.md");

        Assert.IsTrue(File.Exists(reportPath), "The application simplification audit report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Application Simplification Audit");
        StringAssert.Contains(report, "## CDC Duplication Findings");
        StringAssert.Contains(report, "## Redundant Services");
        StringAssert.Contains(report, "## DTO Calculation Leakage");
        StringAssert.Contains(report, "## Safe Simplification Opportunities");
        StringAssert.Contains(report, "## Resolved Sprint Commitment Simplifications");
        StringAssert.Contains(report, "## Estimated Impact");
        StringAssert.Contains(report, "No `PoTool.Application` project exists");
        StringAssert.Contains(report, "CDC duplication");
        StringAssert.Contains(report, "adapter logic (valid)");
        StringAssert.Contains(report, "presentation logic (valid)");
        StringAssert.Contains(report, "transport compatibility");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandler.cs");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "RoadmapAnalyticsService.cs");
        StringAssert.Contains(report, "SprintExecutionDtos.cs");
        StringAssert.Contains(report, "SprintFactResult");
        StringAssert.Contains(report, "DeliveryTrendProgressRollupMapper.cs");
        StringAssert.Contains(report, "RemainingStoryPoints");
        StringAssert.Contains(report, "CompletionPercent");
        StringAssert.Contains(report, "NetFlowStoryPoints");
        StringAssert.Contains(report, "candidate for thin adapter conversion");
        StringAssert.Contains(report, "150–250 lines");
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
