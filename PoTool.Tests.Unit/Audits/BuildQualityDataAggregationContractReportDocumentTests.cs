namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityDataAggregationContractReportDocumentTests
{
    [TestMethod]
    public void BuildQualityDataAggregationContractReport_ReportExistsWithRequiredSectionsAndLockedContent()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "buildquality_data_aggregation_contract_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality data and aggregation contract report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Prompt 2 — BuildQuality Data & Aggregation Contract Report");
        StringAssert.Contains(report, "## 1. Purpose");
        StringAssert.Contains(report, "## 2. In scope");
        StringAssert.Contains(report, "## 3. Out of scope");
        StringAssert.Contains(report, "## 4. Locked decisions applied");
        StringAssert.Contains(report, "## 5. Raw data requirements");
        StringAssert.Contains(report, "## 6. Mapping (Raw → Canonical)");
        StringAssert.Contains(report, "## 7. Aggregation rules");
        StringAssert.Contains(report, "## 8. Time scoping");
        StringAssert.Contains(report, "## 9. Missing data handling");
        StringAssert.Contains(report, "## 10. Data integrity risks");
        StringAssert.Contains(report, "## 11. Consistency with CDC Report");
        StringAssert.Contains(report, "## 12. Drift check");
        StringAssert.Contains(report, "## 13. Open questions introduced");

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
        StringAssert.Contains(report, "percentages MUST NOT be averaged");
        StringAssert.Contains(report, "UNCERTAIN");
        StringAssert.Contains(report, "no additional metrics are introduced");
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
