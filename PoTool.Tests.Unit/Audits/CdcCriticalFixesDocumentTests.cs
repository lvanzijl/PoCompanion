namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcCriticalFixesDocumentTests
{
    [TestMethod]
    public void CdcCriticalFixes_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "cdc-critical-fixes.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC critical fixes report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Critical Fixes");
        StringAssert.Contains(report, "## Summary of fixes");
        StringAssert.Contains(report, "## Before vs after architecture");
        StringAssert.Contains(report, "## Persistence model changes");
        StringAssert.Contains(report, "## Empty snapshot handling");
        StringAssert.Contains(report, "## Ordering guarantees");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Migration notes");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks");
        StringAssert.Contains(report, "PortfolioSnapshotCaptureOrchestrator");
        StringAssert.Contains(report, "write-on-read");
        StringAssert.Contains(report, "unique");
        StringAssert.Contains(report, "empty snapshot");
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
