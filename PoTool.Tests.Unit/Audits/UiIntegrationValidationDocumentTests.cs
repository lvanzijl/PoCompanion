namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class UiIntegrationValidationDocumentTests
{
    [TestMethod]
    public void UiIntegrationValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "ui-integration-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The UI integration validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# UI Integration Validation");
        StringAssert.Contains(report, "## Updated pages and components");
        StringAssert.Contains(report, "## Reusable display components");
        StringAssert.Contains(report, "## Canonical DTO consumption points");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Remaining compatibility constraints");
        StringAssert.Contains(report, "SprintTrend.razor");
        StringAssert.Contains(report, "ProductDeliveryAnalyticsDto");
        StringAssert.Contains(report, "EpicProgressDto");
        StringAssert.Contains(report, "FeatureProgressDto");
        StringAssert.Contains(report, "CanonicalNullablePercentage");
        StringAssert.Contains(report, "CanonicalPlanningQuality");
        StringAssert.Contains(report, "CanonicalInsightList");
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
