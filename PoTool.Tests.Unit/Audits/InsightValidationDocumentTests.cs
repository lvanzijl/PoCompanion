namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class InsightValidationDocumentTests
{
    [TestMethod]
    public void InsightValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analyze", "insight-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The insight validation report should exist under docs/analyze.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Insight Validation");
        StringAssert.Contains(report, "## Implementation summary");
        StringAssert.Contains(report, "## Mapping logic");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Examples");
        StringAssert.Contains(report, "IInsightService");
        StringAssert.Contains(report, "InsightService");
        StringAssert.Contains(report, "InsightResult");
        StringAssert.Contains(report, "Insight");
        StringAssert.Contains(report, "IN-1");
        StringAssert.Contains(report, "IN-7");
        StringAssert.Contains(report, "InsightServiceTests");
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
