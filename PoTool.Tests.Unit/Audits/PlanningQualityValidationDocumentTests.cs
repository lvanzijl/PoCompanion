namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PlanningQualityValidationDocumentTests
{
    [TestMethod]
    public void PlanningQualityValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "planning-quality-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The planning quality validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Planning Quality Validation");
        StringAssert.Contains(report, "## What was added");
        StringAssert.Contains(report, "## Signals implemented");
        StringAssert.Contains(report, "## Scoring logic");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Examples");
        StringAssert.Contains(report, "IPlanningQualityService");
        StringAssert.Contains(report, "PlanningQualityService");
        StringAssert.Contains(report, "PlanningQualityResult");
        StringAssert.Contains(report, "PlanningQualitySignal");
        StringAssert.Contains(report, "PQ-1");
        StringAssert.Contains(report, "PQ-7");
        StringAssert.Contains(report, "PlanningQualityServiceTests");
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
