namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class EpicAggregationNullSemanticsFixDocumentTests
{
    [TestMethod]
    public void EpicAggregationNullSemanticsFix_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "epic-aggregation-null-semantics-fix.md");

        Assert.IsTrue(File.Exists(reportPath), "The epic aggregation null semantics fix report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Epic Aggregation Null Semantics Fix");
        StringAssert.Contains(report, "## Exact places corrected");
        StringAssert.Contains(report, "## Null semantics preservation");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Legacy compatibility constraints");
        StringAssert.Contains(report, "EpicProgress");
        StringAssert.Contains(report, "ProgressPercent");
        StringAssert.Contains(report, "DeliveryProgressRollupService");
        StringAssert.Contains(report, "EpicAggregationNullSemanticsFixDocumentTests");
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
