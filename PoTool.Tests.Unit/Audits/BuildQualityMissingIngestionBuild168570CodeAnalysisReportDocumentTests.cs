namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityMissingIngestionBuild168570CodeAnalysisReportDocumentTests
{
    [TestMethod]
    public void BuildQualityMissingIngestionBuild168570CodeAnalysisReport_ReportExistsWithRequiredSectionsAndDiagnosis()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(
            repositoryRoot,
            "docs",
            "analysis",
            "buildquality-missing-ingestion-build-168570-code-analysis-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality ingestion code analysis report for build 168570 should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality Missing Ingestion For Build 168570 — Code Analysis Report");
        StringAssert.Contains(report, "## 1. Proven facts");
        StringAssert.Contains(report, "## 2. Build-id batch construction analysis");
        StringAssert.Contains(report, "## 3. Test retrieval analysis");
        StringAssert.Contains(report, "## 4. Coverage retrieval analysis");
        StringAssert.Contains(report, "## 5. Pre-persistence filtering analysis");
        StringAssert.Contains(report, "## 6. Logging / observability analysis");
        StringAssert.Contains(report, "## 7. Most likely root cause");
        StringAssert.Contains(report, "## 8. Fix direction");

        StringAssert.Contains(report, "Build `168570` is on `refs/heads/main`");
        StringAssert.Contains(report, "`CachedPipelineRuns` contains a build anchor row for external build id `168570`");
        StringAssert.Contains(report, "`TestRuns` contains **zero** rows linked to that cached build");
        StringAssert.Contains(report, "`Coverages` contains **zero** rows linked to that cached build");
        StringAssert.Contains(report, "GetPipelineRunsAsync");
        StringAssert.Contains(report, "requestedRunIds");
        StringAssert.Contains(report, "top: 100");
        StringAssert.Contains(report, "GetTestRunsByBuildIdsAsync");
        StringAssert.Contains(report, "_apis/testresults/runs?minLastUpdatedDate=");
        StringAssert.Contains(report, "GetCoverageByBuildIdsAsync");
        StringAssert.Contains(report, "_apis/testresults/codecoverage?buildId=<buildId>");
        StringAssert.Contains(report, "\"Line\" or \"Lines\"");
        StringAssert.Contains(report, "case-insensitive");
        StringAssert.Contains(report, "\"missing stable external id\"");
        StringAssert.Contains(report, "The single most likely exact failure point is:");
        StringAssert.Contains(report, "constructs the child-ingestion build batch only from the current `GetPipelineRunsAsync(...)` result set");
        StringAssert.Contains(report, "cached build anchors in the current product/pipeline scope that still have no `TestRuns` and no `Coverages`");
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
