namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PhaseECorrectionsDocumentTests
{
    [TestMethod]
    public void PhaseECorrections_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-e-corrections.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase E corrections report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase E Corrections");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Corrections applied");
        StringAssert.Contains(report, "### 1. Snapshot progress scale");
        StringAssert.Contains(report, "### 2. Timestamp ownership");
        StringAssert.Contains(report, "### 3. Comparison semantics");
        StringAssert.Contains(report, "### 4. Strict validation");
        StringAssert.Contains(report, "### 5. Optional structural cleanup");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Test updates");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks");
        StringAssert.Contains(report, "0..1");
        StringAssert.Contains(report, "delta = null");
        StringAssert.Contains(report, "Timestamp is owned by `PortfolioSnapshot` only");
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
