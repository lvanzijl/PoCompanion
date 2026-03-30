namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class InsightNullSemanticsFixDocumentTests
{
    [TestMethod]
    public void InsightNullSemanticsFix_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "insight-null-semantics-fix.md");

        Assert.IsTrue(File.Exists(reportPath), "The insight null semantics fix report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Insight Null Semantics Fix");
        StringAssert.Contains(report, "## Exact code changes");
        StringAssert.Contains(report, "## Updated mapping logic");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Before vs after examples");
        StringAssert.Contains(report, "InsightService");
        StringAssert.Contains(report, "IN-1");
        StringAssert.Contains(report, "IN-8");
        StringAssert.Contains(report, "ProgressDelta");
        StringAssert.Contains(report, "InsightNullSemanticsFixDocumentTests");
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
