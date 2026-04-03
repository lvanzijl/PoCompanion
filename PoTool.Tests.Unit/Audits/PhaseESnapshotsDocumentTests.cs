namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PhaseESnapshotsDocumentTests
{
    [TestMethod]
    public void PhaseESnapshots_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-e-snapshots.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase E snapshots report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase E Snapshot Model & Comparison Engine");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Snapshot model details");
        StringAssert.Contains(report, "## Validation rules enforced");
        StringAssert.Contains(report, "## Comparison contract");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks for next phase");
        StringAssert.Contains(report, "PortfolioSnapshot");
        StringAssert.Contains(report, "PortfolioSnapshotItem");
        StringAssert.Contains(report, "PortfolioSnapshotComparisonService");
        StringAssert.Contains(report, "Exact business key");
        StringAssert.Contains(report, "delta = null");
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
