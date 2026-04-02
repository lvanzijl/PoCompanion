namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class FeatureForecastValidationDocumentTests
{
    [TestMethod]
    public void FeatureForecastValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "feature-forecast-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The feature forecast validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Feature Forecast Validation");
        StringAssert.Contains(report, "## What was added");
        StringAssert.Contains(report, "## Single source of truth");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Follow-up work");
        StringAssert.Contains(report, "IFeatureForecastService");
        StringAssert.Contains(report, "FeatureForecastService");
        StringAssert.Contains(report, "ForecastConsumedEffort");
        StringAssert.Contains(report, "ForecastRemainingEffort");
        StringAssert.Contains(report, "FeatureForecastServiceTests");
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
