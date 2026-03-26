namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcCorrectionsDocumentTests
{
    [TestMethod]
    public void CdcCorrections_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analyze", "cdc-corrections.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC corrections audit report should exist under docs/analyze.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Corrective CDC Integration Audit");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## 1. Planning Quality Boundary Correction");
        StringAssert.Contains(report, "## 2. Blocking vs Non-Blocking Rule Model");
        StringAssert.Contains(report, "## 3. Snapshot Risk Recalibration");
        StringAssert.Contains(report, "## 4. EstimationMode Contract");
        StringAssert.Contains(report, "## 5. Logging Semantics");
        StringAssert.Contains(report, "## 6. Data Maturity Feedback Loop");
        StringAssert.Contains(report, "## 7. Corrections to Previous Audit");
        StringAssert.Contains(report, "## 8. Final Recommendations");
        StringAssert.Contains(report, "PlanningQualityService");
        StringAssert.Contains(report, "QualitySignalDto");
        StringAssert.Contains(report, "IsBlocking");
        StringAssert.Contains(report, "EstimationMode");
        StringAssert.Contains(report, "WorkPackage");
        StringAssert.Contains(report, "SnapshotComparisonService");
        StringAssert.Contains(report, "RuleCatalog");
        StringAssert.Contains(report, "BacklogValidationService");
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
