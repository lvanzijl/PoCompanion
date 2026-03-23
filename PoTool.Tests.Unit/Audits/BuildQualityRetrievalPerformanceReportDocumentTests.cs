namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityRetrievalPerformanceReportDocumentTests
{
    [TestMethod]
    public void BuildQualityRetrievalPerformanceReport_ReportExistsWithRequiredSectionsAndLimitations()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "buildquality_retrieval_performance_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality retrieval performance report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality Retrieval Performance Report");
        StringAssert.Contains(report, "## 1. Scope measured");
        StringAssert.Contains(report, "## 2. Phase timings");
        StringAssert.Contains(report, "## 3. Request volumes");
        StringAssert.Contains(report, "## 4. Slowest test-run builds");
        StringAssert.Contains(report, "## 5. Bottleneck assessment");
        StringAssert.Contains(report, "## 6. Recommended next optimization");
        StringAssert.Contains(report, "mock mode");
        StringAssert.Contains(report, "RealTfsClient");
        StringAssert.Contains(report, "428");
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
