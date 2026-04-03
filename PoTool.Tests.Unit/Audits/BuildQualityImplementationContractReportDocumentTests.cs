namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class BuildQualityImplementationContractReportDocumentTests
{
    [TestMethod]
    public void BuildQualityImplementationContractReport_ReportExistsWithRequiredSectionsAndLockedContent()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality-implementation-contract-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality implementation contract report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Prompt 4 — BuildQuality Data Foundation (Ingestion & Persistence Contract Report)");
        StringAssert.Contains(report, "## 1. Purpose");
        StringAssert.Contains(report, "## 2. In scope");
        StringAssert.Contains(report, "## 3. Out of scope");
        StringAssert.Contains(report, "## 4. Locked decisions applied");
        StringAssert.Contains(report, "## 5. TFS client contract (`ITfsClient`)");
        StringAssert.Contains(report, "## 6. External data contracts");
        StringAssert.Contains(report, "## 7. Sync-stage responsibilities");
        StringAssert.Contains(report, "## 8. Persistence model");
        StringAssert.Contains(report, "## 9. Linkage model");
        StringAssert.Contains(report, "## 10. Data integrity rules");
        StringAssert.Contains(report, "## 11. Unknown propagation support");
        StringAssert.Contains(report, "## 12. Performance considerations");
        StringAssert.Contains(report, "## 13. Integration risks");
        StringAssert.Contains(report, "## 14. Consistency with previous reports");
        StringAssert.Contains(report, "## 15. Drift check");
        StringAssert.Contains(report, "## 16. Open questions");

        StringAssert.Contains(report, "Option A — Expand scope is mandatory.");
        StringAssert.Contains(report, "full BuildQuality scope required");
        StringAssert.Contains(report, "no build-only fallback");
        StringAssert.Contains(report, "formulas unchanged");
        StringAssert.Contains(report, "Unknown rules remain unchanged");
        StringAssert.Contains(report, "CDC not reinterpreted");
        StringAssert.Contains(report, "aggregation remains unchanged");
        StringAssert.Contains(report, "no new metrics");
        StringAssert.Contains(report, "ITfsClient");
        StringAssert.Contains(report, "GetTestRunsByBuildIdsAsync");
        StringAssert.Contains(report, "GetCoverageByBuildIdsAsync");
        StringAssert.Contains(report, "BuildId");
        StringAssert.Contains(report, "TotalTests");
        StringAssert.Contains(report, "PassedTests");
        StringAssert.Contains(report, "NotApplicableTests");
        StringAssert.Contains(report, "CoveredLines");
        StringAssert.Contains(report, "TotalLines");
        StringAssert.Contains(report, "Cobertura or equivalent formats are expected");
        StringAssert.Contains(report, "pipeline defines filters (not ingestion)");
        StringAssert.Contains(report, "exact TFS field names for total, passed, and notApplicable remain **UNCERTAIN**");
        StringAssert.Contains(report, "exact TFS field names or artifact element names for coverage totals remain **UNCERTAIN**");
        StringAssert.Contains(report, "exact TFS test-run source field names for `TotalTests`, `PassedTests`, and `NotApplicableTests` remain **UNCERTAIN**");
        StringAssert.Contains(report, "raw facts only");
        StringAssert.Contains(report, "build is the anchor");
        StringAssert.Contains(report, "multiple test runs per build are allowed");
        StringAssert.Contains(report, "multiple coverage entries per build are allowed");
        StringAssert.Contains(report, "missing test runs are allowed");
        StringAssert.Contains(report, "missing coverage is allowed");
        StringAssert.Contains(report, "no coercion to zero");
        StringAssert.Contains(report, "incomplete records must not break ingestion");
        StringAssert.Contains(report, "avoid N+1 calls per build");
        StringAssert.Contains(report, "BuildQuality provider contract remains valid");
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
