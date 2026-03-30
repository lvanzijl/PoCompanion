namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class EpicAggregationValidationDocumentTests
{
    [TestMethod]
    public void EpicAggregationValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "epic-aggregation-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The epic aggregation validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Epic Aggregation Validation");
        StringAssert.Contains(report, "## What was added");
        StringAssert.Contains(report, "## Single source of truth");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Follow-up work");
        StringAssert.Contains(report, "IEpicAggregationService");
        StringAssert.Contains(report, "EpicAggregationService");
        StringAssert.Contains(report, "EpicAggregationResult");
        StringAssert.Contains(report, "ExcludedFeaturesCount");
        StringAssert.Contains(report, "DeliveryProgressRollupServiceTests");
        StringAssert.Contains(report, "EpicAggregationServiceTests");
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
