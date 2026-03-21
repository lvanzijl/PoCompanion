namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityImplementationContractReportDocumentTests
{
    [TestMethod]
    public void BuildQualityImplementationContractReport_ReportExistsWithRequiredSectionsAndLockedContent()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "buildquality_implementation_contract_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality implementation contract report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Prompt 4 — BuildQuality Implementation Contract Report");
        StringAssert.Contains(report, "## 1. Purpose");
        StringAssert.Contains(report, "## 2. In scope");
        StringAssert.Contains(report, "## 3. Out of scope");
        StringAssert.Contains(report, "## 4. Locked decisions applied");
        StringAssert.Contains(report, "## 5. Query contracts (PoTool.Core)");
        StringAssert.Contains(report, "## 6. Handler design (PoTool.Api)");
        StringAssert.Contains(report, "## 7. DTO definitions (PoTool.Shared)");
        StringAssert.Contains(report, "## 8. Endpoint definitions (PoTool.Api)");
        StringAssert.Contains(report, "## 9. Client consumption (PoTool.Client)");
        StringAssert.Contains(report, "## 10. Data flow (implementation view)");
        StringAssert.Contains(report, "## 11. Single source of truth enforcement");
        StringAssert.Contains(report, "## 12. Integration risks");
        StringAssert.Contains(report, "## 13. Consistency with previous reports");
        StringAssert.Contains(report, "## 14. Drift check");
        StringAssert.Contains(report, "## 15. Open questions");

        StringAssert.Contains(report, "default branch only");
        StringAssert.Contains(report, "no nightly builds");
        StringAssert.Contains(report, "no feature branches");
        StringAssert.Contains(report, "count-first / totals-first");
        StringAssert.Contains(report, "percentages MUST NOT be averaged");
        StringAssert.Contains(report, "GetBuildQualityRollingWindowQuery");
        StringAssert.Contains(report, "GetBuildQualitySprintQuery");
        StringAssert.Contains(report, "GetBuildQualityPipelineDetailQuery");
        StringAssert.Contains(report, "one shared BuildQuality provider/service");
        StringAssert.Contains(report, "handlers MUST NOT implement formulas");
        StringAssert.Contains(report, "BuildQualityMetricsDto");
        StringAssert.Contains(report, "BuildQualityEvidenceDto");
        StringAssert.Contains(report, "BuildQualityResultDto");
        StringAssert.Contains(report, "BuildQualityProductDto");
        StringAssert.Contains(report, "BuildQualityPageDto");
        StringAssert.Contains(report, "DeliveryBuildQualityDto");
        StringAssert.Contains(report, "PipelineBuildQualityDto");
        StringAssert.Contains(report, "GET /api/buildquality/rolling");
        StringAssert.Contains(report, "GET /api/buildquality/sprint");
        StringAssert.Contains(report, "GET /api/buildquality/pipeline");
        StringAssert.Contains(report, "no mediator usage in `PoTool.Client`");
        StringAssert.Contains(report, "no direct HTTP logic in pages");
        StringAssert.Contains(report, "The shared BuildQuality provider is the ONLY place where:");
        StringAssert.Contains(report, "formulas unchanged");
        StringAssert.Contains(report, "Unknown rules unchanged");
        StringAssert.Contains(report, "no new metrics");
        StringAssert.Contains(report, "CDC not reinterpreted");
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
