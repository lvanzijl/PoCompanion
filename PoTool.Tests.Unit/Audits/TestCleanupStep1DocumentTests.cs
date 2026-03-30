namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class TestCleanupStep1DocumentTests
{
    [TestMethod]
    public void TestCleanupStep1_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "test_cleanup_step1.md");

        Assert.IsTrue(File.Exists(reportPath), "The test cleanup step 1 report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Test Cleanup Step 1");
        StringAssert.Contains(report, "## Files Updated");
        StringAssert.Contains(report, "## Semantic Assertions Removed");
        StringAssert.Contains(report, "## Orchestration Tests Retained");
        StringAssert.Contains(report, "## CDC Tests Strengthened");
        StringAssert.Contains(report, "## Remaining Duplicate Risk");
        StringAssert.Contains(report, "HistoricalSprintLookupTests.cs");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetCapacityCalibrationQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEpicCompletionForecastQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEffortDistributionTrendQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEffortImbalanceQueryHandlerTests.cs");
        StringAssert.Contains(report, "SprintCommitmentCdcServicesTests.cs");
        StringAssert.Contains(report, "ForecastingDomainServicesTests.cs");
        StringAssert.Contains(report, "EffortDiagnosticsDomainModelsTests.cs");
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
