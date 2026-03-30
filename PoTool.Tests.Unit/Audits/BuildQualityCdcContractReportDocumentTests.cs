namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityCdcContractReportDocumentTests
{
    [TestMethod]
    public void BuildQualityCdcContractReport_ReportExistsWithRequiredSectionsAndLockedDecisions()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality_cdc_contract_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality CDC contract report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Prompt 1 — BuildQuality CDC Contract Report");
        StringAssert.Contains(report, "## 1. Purpose");
        StringAssert.Contains(report, "## 2. In scope");
        StringAssert.Contains(report, "## 3. Out of scope");
        StringAssert.Contains(report, "## 4. Locked decisions applied");
        StringAssert.Contains(report, "## 5. BuildQuality definition");
        StringAssert.Contains(report, "## 6. CDC positioning");
        StringAssert.Contains(report, "## 7. Metrics");
        StringAssert.Contains(report, "## 8. Formulas");
        StringAssert.Contains(report, "## 9. Unknown handling");
        StringAssert.Contains(report, "## 10. Confidence model");
        StringAssert.Contains(report, "## 11. Time semantics");
        StringAssert.Contains(report, "## 12. CDC compliance explanation");
        StringAssert.Contains(report, "## 13. Drift check");
        StringAssert.Contains(report, "## 14. Open questions introduced");

        StringAssert.Contains(report, "default branch only");
        StringAssert.Contains(report, "no nightly-build aggregation");
        StringAssert.Contains(report, "no feature-branch aggregation");
        StringAssert.Contains(report, "minimum_builds = 3");
        StringAssert.Contains(report, "minimum_tests = 20");
        StringAssert.Contains(report, "SuccessRate");
        StringAssert.Contains(report, "TestPassRate");
        StringAssert.Contains(report, "TestVolume");
        StringAssert.Contains(report, "Coverage");
        StringAssert.Contains(report, "Confidence");
        StringAssert.Contains(report, "BuildThresholdMet");
        StringAssert.Contains(report, "TestThresholdMet");
        StringAssert.Contains(report, "Confidence = BuildThresholdMet + TestThresholdMet");
        StringAssert.Contains(report, "No drift detected.");
        StringAssert.Contains(report, "None.");
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
