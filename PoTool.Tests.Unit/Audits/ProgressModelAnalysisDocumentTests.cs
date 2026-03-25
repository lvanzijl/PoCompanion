namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ProgressModelAnalysisDocumentTests
{
    [TestMethod]
    public void ProgressModelAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analyze", "progress-model.md");

        Assert.IsTrue(File.Exists(reportPath), "The progress model analysis report should exist under docs/analyze.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Progress Model & Override Integration Analysis");
        StringAssert.Contains(report, "## 2. Current progress calculations");
        StringAssert.Contains(report, "## 3. StoryPoints usage across the stack");
        StringAssert.Contains(report, "## 4. Progress fields and percentage rendering");
        StringAssert.Contains(report, "## 5. Manual override — current state");
        StringAssert.Contains(report, "## 6. Gaps vs desired model");
        StringAssert.Contains(report, "## 7. Integration strategy");
        StringAssert.Contains(report, "DeliveryProgressRollupService");
        StringAssert.Contains(report, "HierarchyRollupService");
        StringAssert.Contains(report, "ProgressMode");
        StringAssert.Contains(report, "TimeCriticality");
        StringAssert.Contains(report, "EffectiveProgress");
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
