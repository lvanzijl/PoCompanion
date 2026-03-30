namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PlanningQualityAnalysisDocumentTests
{
    [TestMethod]
    public void PlanningQualityAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "planning-quality.md");

        Assert.IsTrue(File.Exists(reportPath), "The planning quality analysis report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Planning Quality & Signal Integration Analysis");
        StringAssert.Contains(report, "## 1. Current severity model");
        StringAssert.Contains(report, "## 2. Warning vs error distinction");
        StringAssert.Contains(report, "## 3. Signal aggregation");
        StringAssert.Contains(report, "## 4. Integration options for Planning Quality");
        StringAssert.Contains(report, "## 5. Gaps");
        StringAssert.Contains(report, "## 6. Recommendations");
        StringAssert.Contains(report, "RuleFindingClass");
        StringAssert.Contains(report, "RuleFamily");
        StringAssert.Contains(report, "ValidationCategory");
        StringAssert.Contains(report, "ValidationCategoryMeta");
        StringAssert.Contains(report, "EFF");
        StringAssert.Contains(report, "BacklogHealthCalculator");
        StringAssert.Contains(report, "PlanningQuality");
        StringAssert.Contains(report, "PQ-1");
        StringAssert.Contains(report, "BudgetSnapshotEntity");
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
