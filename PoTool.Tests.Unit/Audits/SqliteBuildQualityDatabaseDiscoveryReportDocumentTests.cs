namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class SqliteBuildQualityDatabaseDiscoveryReportDocumentTests
{
    [TestMethod]
    public void SqliteBuildQualityDatabaseDiscoveryReport_ReportExistsWithRequiredSectionsAndKeyFacts()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "sqlite-buildquality-database-discovery-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The SQLite BuildQuality database discovery report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# SQLite BuildQuality Database Discovery Report");
        StringAssert.Contains(report, "## 1. Database overview");
        StringAssert.Contains(report, "## 2. Relevant BuildQuality tables");
        StringAssert.Contains(report, "## 3. Build anchor model");
        StringAssert.Contains(report, "## 4. Table-by-table column reference");
        StringAssert.Contains(report, "## 5. Linkage explanation");
        StringAssert.Contains(report, "## 6. Manual SQLite query cookbook");
        StringAssert.Contains(report, "## 7. BuildQuality-specific debugging path");
        StringAssert.Contains(report, "## 8. Risks / caveats");
        StringAssert.Contains(report, "## 9. Final summary");

        StringAssert.Contains(report, "potool.db");
        StringAssert.Contains(report, "CachedPipelineRuns");
        StringAssert.Contains(report, "TestRuns");
        StringAssert.Contains(report, "Coverages");
        StringAssert.Contains(report, "TfsRunId");
        StringAssert.Contains(report, "CachedPipelineRuns.Id");
        StringAssert.Contains(report, "TestRuns.BuildId");
        StringAssert.Contains(report, "Coverages.BuildId");
        StringAssert.Contains(report, "PRAGMA table_info('CachedPipelineRuns')");
        StringAssert.Contains(report, "WHERE TfsRunId = 168570");
        StringAssert.Contains(report, "Multiple test runs per build are allowed.");
        StringAssert.Contains(report, "Multiple coverage rows per build are allowed.");
        StringAssert.Contains(report, "UNCERTAIN");
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
