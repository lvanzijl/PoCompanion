namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ValidationRulesAnalysisDocumentTests
{
    [TestMethod]
    public void ValidationRulesAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs/analyze/validation-rules.md");

        Assert.IsTrue(File.Exists(reportPath), "The validation rules analysis report should exist under docs/analyze.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Validation, Integrity, and Health Rules Analysis");
        StringAssert.Contains(report, "## 1. Current health providers and entry points");
        StringAssert.Contains(report, "## 2. Full inventory of current rules");
        StringAssert.Contains(report, "## 3. Current categorization and execution model");
        StringAssert.Contains(report, "## 4. Overlap and duplicate logic");
        StringAssert.Contains(report, "## 5. Gaps vs desired Integrity / Planning Quality model");
        StringAssert.Contains(report, "## 6. Refactoring recommendations");
        StringAssert.Contains(report, "SI-1");
        StringAssert.Contains(report, "RC-2");
        StringAssert.Contains(report, "EFF");
        StringAssert.Contains(report, "BacklogHealthCalculator");
        StringAssert.Contains(report, "HierarchicalWorkItemValidator");
        StringAssert.Contains(report, "BacklogValidationService");
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
