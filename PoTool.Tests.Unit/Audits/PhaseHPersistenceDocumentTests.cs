namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PhaseHPersistenceDocumentTests
{
    [TestMethod]
    public void PhaseHPersistence_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-h-persistence.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase H persistence report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase H Snapshot Persistence & Selection Policy");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Persistence model");
        StringAssert.Contains(report, "## Selection policy");
        StringAssert.Contains(report, "## Validation/persistence flow");
        StringAssert.Contains(report, "## Integrity handling");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks for Phase I");
        StringAssert.Contains(report, "PortfolioSnapshotEntity");
        StringAssert.Contains(report, "PortfolioSnapshotSelectionService");
        StringAssert.Contains(report, "archived");
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
