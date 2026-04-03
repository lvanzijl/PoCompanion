namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PhaseBCorrectionsDocumentTests
{
    [TestMethod]
    public void PhaseBCorrections_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-b-corrections.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase B corrections report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase B Corrections");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Corrections applied per section");
        StringAssert.Contains(report, "### 1. Remove forecasting coupling");
        StringAssert.Contains(report, "### 2. Enforce canonical work item typing");
        StringAssert.Contains(report, "### 3. Define and enforce TimeCriticality validation");
        StringAssert.Contains(report, "### 4. Narrow FeatureProgressService contract");
        StringAssert.Contains(report, "### 5. Preserve determinism and ordering independence");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Test coverage updates");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Remaining risks (if any)");
        StringAssert.Contains(report, "TimeCriticality");
        StringAssert.Contains(report, "FeatureProgressService");
        StringAssert.Contains(report, "canonical");
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
