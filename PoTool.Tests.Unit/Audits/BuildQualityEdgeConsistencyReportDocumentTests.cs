namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityEdgeConsistencyReportDocumentTests
{
    [TestMethod]
    public void BuildQualityEdgeConsistencyReport_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality-edge-consistency-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality edge consistency report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality Edge Case & Cross-Workspace Consistency Report");
        StringAssert.Contains(report, "## 1. Scenarios validated");
        StringAssert.Contains(report, "## 2. Cross-page comparison");
        StringAssert.Contains(report, "## 3. Binding validation");
        StringAssert.Contains(report, "## 4. Logging validation");
        StringAssert.Contains(report, "## 5. Issues found");
        StringAssert.Contains(report, "## 6. Final verdict");
        StringAssert.Contains(report, "Scenario A");
        StringAssert.Contains(report, "Scenario B");
        StringAssert.Contains(report, "Scenario C");
        StringAssert.Contains(report, "Scenario D");
        StringAssert.Contains(report, "910001");
        StringAssert.Contains(report, "910003");
        StringAssert.Contains(report, "910004");
        StringAssert.Contains(report, "910005");
        StringAssert.Contains(report, "Health rolling summary == Delivery sprint summary");
        StringAssert.Contains(report, "Health Product B == Delivery Product B == Pipeline Insights Pipeline B");
        StringAssert.Contains(report, "BUILDQUALITY_TESTRUN_MISSING_DATA");
        StringAssert.Contains(report, "BUILDQUALITY_COVERAGE_MISSING_DATA");
        StringAssert.Contains(report, "READY WITH FIXES");
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
