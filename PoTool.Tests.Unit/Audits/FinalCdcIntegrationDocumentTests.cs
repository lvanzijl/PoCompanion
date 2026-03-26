namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class FinalCdcIntegrationDocumentTests
{
    [TestMethod]
    public void FinalCdcIntegration_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analyze", "final-cdc-integration.md");

        Assert.IsTrue(File.Exists(reportPath), "The final CDC integration audit report should exist under docs/analyze.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Final CDC Integration Audit & Implementation Plan");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Consistency Audit");
        StringAssert.Contains(report, "## Conflicts");
        StringAssert.Contains(report, "## Missing Pieces");
        StringAssert.Contains(report, "## Implementation Plan");
        StringAssert.Contains(report, "## Risk Table");
        StringAssert.Contains(report, "Phase A");
        StringAssert.Contains(report, "Phase B");
        StringAssert.Contains(report, "Phase C");
        StringAssert.Contains(report, "Phase D");
        StringAssert.Contains(report, "Phase E");
        StringAssert.Contains(report, "Phase F");
        StringAssert.Contains(report, "TimeCriticality");
        StringAssert.Contains(report, "ProjectNumber");
        StringAssert.Contains(report, "WorkPackage");
        StringAssert.Contains(report, "EffectiveProgress");
        StringAssert.Contains(report, "StateClassification");
        StringAssert.Contains(report, "PlanningQuality");
        StringAssert.Contains(report, "SnapshotComparisonService");
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
