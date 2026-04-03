namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class TestOwnershipAuditDocumentTests
{
    [TestMethod]
    public void TestOwnershipAudit_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "test-ownership-audit.md");

        Assert.IsTrue(File.Exists(reportPath), "The test ownership audit report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Test Ownership Audit");
        StringAssert.Contains(report, "PoTool.Tests.Unit");
        StringAssert.Contains(report, "## CDC Semantic Tests Already Correct");
        StringAssert.Contains(report, "## Tests That Belong in CDC");
        StringAssert.Contains(report, "## Duplicate Semantic Tests");
        StringAssert.Contains(report, "## Valid Application Tests");
        StringAssert.Contains(report, "## Migration Candidates");

        StringAssert.Contains(report, "SprintCommitmentCdcServicesTests.cs");
        StringAssert.Contains(report, "HistoricalSprintLookupTests.cs");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetCapacityCalibrationQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEpicCompletionForecastQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEffortDistributionTrendQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEffortImbalanceQueryHandlerTests.cs");
        StringAssert.Contains(report, "SprintTrendProjectionServiceSqliteTests.cs");
        StringAssert.Contains(report, "handler integration test");
        StringAssert.Contains(report, "projection persistence test");
        StringAssert.Contains(report, "should move");
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
