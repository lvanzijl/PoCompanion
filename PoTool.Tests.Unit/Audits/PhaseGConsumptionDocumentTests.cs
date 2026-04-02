namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PhaseGConsumptionDocumentTests
{
    [TestMethod]
    public void PhaseGConsumption_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-g-consumption.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase G implementation report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase G Consumption Validation");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## DTO design");
        StringAssert.Contains(report, "## Query services");
        StringAssert.Contains(report, "## Filtering behavior");
        StringAssert.Contains(report, "## API endpoints");
        StringAssert.Contains(report, "## UI integration details");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks");
        StringAssert.Contains(report, "PortfolioProgressDto");
        StringAssert.Contains(report, "PortfolioSnapshotDto");
        StringAssert.Contains(report, "PortfolioComparisonDto");
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
