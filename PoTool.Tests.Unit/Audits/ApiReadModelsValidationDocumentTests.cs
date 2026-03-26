namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ApiReadModelsValidationDocumentTests
{
    [TestMethod]
    public void ApiReadModelsValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analyze", "api-readmodels-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The API read-models validation report should exist under docs/analyze.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# API Read Models Validation");
        StringAssert.Contains(report, "## DTOs and read models added or changed");
        StringAssert.Contains(report, "## Mappers and adapters updated");
        StringAssert.Contains(report, "## Handlers and controllers wired to canonical services");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Remaining compatibility constraints");
        StringAssert.Contains(report, "GetSprintTrendMetricsQueryHandler");
        StringAssert.Contains(report, "DeliveryTrendAnalyticsExposureMapper");
        StringAssert.Contains(report, "DeliveryTrendProgressRollupMapper");
        StringAssert.Contains(report, "ProductDeliveryAnalyticsDto");
        StringAssert.Contains(report, "ProductProgressSummaryDto");
        StringAssert.Contains(report, "SnapshotComparisonDto");
        StringAssert.Contains(report, "PlanningQualityDto");
        StringAssert.Contains(report, "InsightDto");
        StringAssert.Contains(report, "GetSprintTrendMetricsQueryHandlerTests");
        StringAssert.Contains(report, "DeliveryTrendAnalyticsExposureMapperTests");
        StringAssert.Contains(report, "DtoContractCleanupTests");
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
