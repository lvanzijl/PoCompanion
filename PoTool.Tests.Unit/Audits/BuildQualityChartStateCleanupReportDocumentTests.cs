namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityChartStateCleanupReportDocumentTests
{
    [TestMethod]
    public void BuildQualityChartStateCleanupReport_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality_chart_state_cleanup_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality chart state cleanup report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality Chart State Cleanup Report");
        StringAssert.Contains(report, "## 1. Purpose");
        StringAssert.Contains(report, "## 2. Findings before cleanup");
        StringAssert.Contains(report, "## 3. Changes made");
        StringAssert.Contains(report, "## 4. Validation performed");
        StringAssert.Contains(report, "## 5. What was intentionally not changed");
        StringAssert.Contains(report, "## 6. Final conclusion");
        StringAssert.Contains(report, "## Reviewer-ready summary");
        StringAssert.Contains(report, "QualityStateLabel");
        StringAssert.Contains(report, "QualityStrokeColor");
        StringAssert.Contains(report, "dormant");
        StringAssert.Contains(report, "no matches found");
        StringAssert.Contains(report, "BuildQuality semantic changes");
        StringAssert.Contains(report, "26 passed");
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
