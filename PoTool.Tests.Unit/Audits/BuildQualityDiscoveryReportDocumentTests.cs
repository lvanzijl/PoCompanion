namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityDiscoveryReportDocumentTests
{
    [TestMethod]
    public void BuildQualityDiscoveryReport_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality-discovery-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality discovery report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Prompt 0 — BuildQuality Discovery Report");
        StringAssert.Contains(report, "## 1. Solution Overview");
        StringAssert.Contains(report, "## 2. CDC (Canonical Domain Core)");
        StringAssert.Contains(report, "## 3. Data Ingestion");
        StringAssert.Contains(report, "## 4. Storage Model");
        StringAssert.Contains(report, "## 5. Application Layer");
        StringAssert.Contains(report, "## 6. Existing Pages");
        StringAssert.Contains(report, "## 7. Cross-cutting Rules");
        StringAssert.Contains(report, "## 8. Multi-product Model");
        StringAssert.Contains(report, "## 9. Gaps for BuildQuality");
        StringAssert.Contains(report, "## 10. Certainty Assessment");

        StringAssert.Contains(report, "PoTool.Core.Domain.Cdc.Sprints");
        StringAssert.Contains(report, "Test-run ingestion is **NOT FOUND**.");
        StringAssert.Contains(report, "Coverage ingestion is **NOT FOUND**.");
        StringAssert.Contains(report, "`/home/health`");
        StringAssert.Contains(report, "`/home/delivery`");
        StringAssert.Contains(report, "`/home/pipeline-insights`");
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
