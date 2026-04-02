namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PhaseCEpicProgressDocumentTests
{
    [TestMethod]
    public void PhaseCEpicProgress_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "phase-c-epic-progress.md");

        Assert.IsTrue(File.Exists(reportPath), "The Phase C epic progress report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Phase C Epic Progress Aggregation");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Implementation details");
        StringAssert.Contains(report, "## Aggregation formula validation");
        StringAssert.Contains(report, "## Test coverage");
        StringAssert.Contains(report, "## Files changed");
        StringAssert.Contains(report, "## Build/test results");
        StringAssert.Contains(report, "## Risks for Phase D");
        StringAssert.Contains(report, "EpicProgressService");
        StringAssert.Contains(report, "Feature");
        StringAssert.Contains(report, "TotalEffort");
        StringAssert.Contains(report, "TimeCriticality");
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
