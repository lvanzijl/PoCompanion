namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ProductAggregationValidationDocumentTests
{
    [TestMethod]
    public void ProductAggregationValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "product-aggregation-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The product aggregation validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Product Aggregation Validation");
        StringAssert.Contains(report, "## What was added");
        StringAssert.Contains(report, "## Single source of truth");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Follow-up work");
        StringAssert.Contains(report, "IProductAggregationService");
        StringAssert.Contains(report, "ProductAggregationService");
        StringAssert.Contains(report, "ProductAggregationResult");
        StringAssert.Contains(report, "ExcludedEpicsCount");
        StringAssert.Contains(report, "ProductAggregationServiceTests");
        StringAssert.Contains(report, "ServiceCollectionTests");
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
