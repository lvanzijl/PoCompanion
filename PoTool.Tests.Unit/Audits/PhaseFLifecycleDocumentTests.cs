namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PhaseFLifecycleDocumentTests
{
    [TestMethod]
    public void PhaseFLifecycle_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-f-lifecycle.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase F lifecycle report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase F Snapshot Lifecycle & Capture Strategy");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Lifecycle model details");
        StringAssert.Contains(report, "## Validation changes");
        StringAssert.Contains(report, "## Capture strategy");
        StringAssert.Contains(report, "## Comparison updates");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks");
        StringAssert.Contains(report, "WorkPackageLifecycleState");
        StringAssert.Contains(report, "PortfolioSnapshotFactory");
        StringAssert.Contains(report, "Active");
        StringAssert.Contains(report, "Retired");
        StringAssert.Contains(report, "deterministic");
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
