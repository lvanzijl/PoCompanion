namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcMockDatasetReportDocumentTests
{
    [TestMethod]
    public void CdcMockDatasetReport_ExistsWithRequiredSectionsAndScenarioCoverage()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "cdc_mock_dataset_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC mock dataset report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Mock Dataset Report");
        StringAssert.Contains(report, "## Dataset structure");
        StringAssert.Contains(report, "## Covered scenarios");
        StringAssert.Contains(report, "## How to run");
        StringAssert.Contains(report, "## What to verify manually");
        StringAssert.Contains(report, "Product A");
        StringAssert.Contains(report, "Product B");
        StringAssert.Contains(report, "Product C");
        StringAssert.Contains(report, "identical timestamps");
        StringAssert.Contains(report, "UnixEpoch");
        StringAssert.Contains(report, "snapshotCount");
        StringAssert.Contains(report, "completed product");
        StringAssert.Contains(report, "CdcTestDataSeeder");
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
