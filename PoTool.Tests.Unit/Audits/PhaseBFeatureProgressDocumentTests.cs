namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PhaseBFeatureProgressDocumentTests
{
    [TestMethod]
    public void PhaseBFeatureProgress_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-b-feature-progress.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase B feature progress report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase B Feature Progress & Override Aggregation");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Implementation details");
        StringAssert.Contains(report, "## Formula validation");
        StringAssert.Contains(report, "## Override behavior validation");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Risks for Phase C");
        StringAssert.Contains(report, "TimeCriticality");
        StringAssert.Contains(report, "Removed");
        StringAssert.Contains(report, "Task");
        StringAssert.Contains(report, "Bug");
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
