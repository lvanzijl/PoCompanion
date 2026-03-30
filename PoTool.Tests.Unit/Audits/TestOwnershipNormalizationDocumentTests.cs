namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class TestOwnershipNormalizationDocumentTests
{
    [TestMethod]
    public void TestOwnershipNormalization_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "test_ownership_normalization.md");

        Assert.IsTrue(File.Exists(reportPath), "The test ownership normalization report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Test Ownership Normalization");
        StringAssert.Contains(report, "## Ownership Rules Applied");
        StringAssert.Contains(report, "## Handler Tests Retained");
        StringAssert.Contains(report, "## CDC Tests Owning Semantics");
        StringAssert.Contains(report, "## Projection / Adapter Tests Retained");
        StringAssert.Contains(report, "## Final Boundary Status");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetCapacityCalibrationQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEpicCompletionForecastQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEffortDistributionTrendQueryHandlerTests.cs");
        StringAssert.Contains(report, "GetEffortImbalanceQueryHandlerTests.cs");
        StringAssert.Contains(report, "SprintCommitmentCdcServicesTests.cs");
        StringAssert.Contains(report, "ForecastingDomainServicesTests.cs");
        StringAssert.Contains(report, "EffortDiagnosticsDomainModelsTests.cs");
        StringAssert.Contains(report, "BacklogReadinessServiceTests.cs");
        StringAssert.Contains(report, "PortfolioFlowProjectionServiceTests.cs");
        StringAssert.Contains(report, "SprintTrendProjectionServiceSqliteTests.cs");
        StringAssert.Contains(report, "DeliveryTrendDomainModelsTests.cs");
        StringAssert.Contains(report, "formulas, invariants, and edge-case semantics");
        StringAssert.Contains(report, "orchestration, filtering, request scoping, and DTO mapping");
        StringAssert.Contains(report, "persistence and deterministic replay outputs");
        StringAssert.Contains(report, "formatting and compatibility mapping");
        StringAssert.Contains(report, "no production code changes were required");
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
