namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowApplicationMigrationDocumentTests
{
    [TestMethod]
    public void PortfolioFlowApplicationMigration_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "portfolio-flow-application-migration.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow application migration report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Application Migration");
        StringAssert.Contains(report, "## Legacy Path Removed");
        StringAssert.Contains(report, "## Canonical Projection Adopted");
        StringAssert.Contains(report, "## DTO Compatibility Decisions");
        StringAssert.Contains(report, "## UI Changes");
        StringAssert.Contains(report, "## Tests Updated");
        StringAssert.Contains(report, "## Remaining Portfolio Work");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "PortfolioProgressTrendDtos.cs");
        StringAssert.Contains(report, "PortfolioProgressPage.razor");
        StringAssert.Contains(report, "PortfolioFlowProjectionEntity");
        StringAssert.Contains(report, "StockStoryPoints");
        StringAssert.Contains(report, "RemainingScopeStoryPoints");
        StringAssert.Contains(report, "InflowStoryPoints");
        StringAssert.Contains(report, "ThroughputStoryPoints");
        StringAssert.Contains(report, "CompletionPercent");
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
