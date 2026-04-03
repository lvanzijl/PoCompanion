namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class CdcFreezeAuditDocumentTests
{
    [TestMethod]
    public void CdcFreezeAudit_ReportExistsWithRequiredSectionsAndFreezeDecision()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = $"{repositoryRoot}/docs/analysis/cdc-freeze-audit.md";

        Assert.IsTrue(File.Exists(reportPath), "The CDC freeze audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Freeze Audit");
        StringAssert.Contains(report, "## Semantic Ownership Status");
        StringAssert.Contains(report, "## Test Ownership Status");
        StringAssert.Contains(report, "## Boundary Cleanliness");
        StringAssert.Contains(report, "## Compatibility Debt Still Present");
        StringAssert.Contains(report, "## Freeze Decision");
        StringAssert.Contains(report, "## Next Phase Recommendation");

        StringAssert.Contains(report, "12 of 14 audited handlers");
        StringAssert.Contains(report, "zero CDC bypass findings");
        StringAssert.Contains(report, "handlers are reduced to loading, orchestration, filtering, and DTO mapping");
        StringAssert.Contains(report, "projection tests own persistence behavior and deterministic replay outputs");
        StringAssert.Contains(report, "EffortEstimationSettingsDto");
        StringAssert.Contains(report, "client-side roadmap scope replay");
        StringAssert.Contains(report, "frozen with known compatibility debt");
        StringAssert.Contains(report, "persistence and source-abstraction work");
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists($"{current.FullName}/PoTool.sln"))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
