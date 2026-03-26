namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PhaseIFinalizationDocumentTests
{
    [TestMethod]
    public void PhaseIFinalization_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-i-finalization.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase I finalization report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase I Finalization Validation");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Trend model");
        StringAssert.Contains(report, "## Decision-signal model");
        StringAssert.Contains(report, "## Query/API changes");
        StringAssert.Contains(report, "## UI integration details");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks");
        StringAssert.Contains(report, "persisted snapshots only");
        StringAssert.Contains(report, "PortfolioTrendDto");
        StringAssert.Contains(report, "PortfolioDecisionSignalDto");
        StringAssert.Contains(report, "/api/portfolio/trends");
        StringAssert.Contains(report, "/api/portfolio/signals");
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
