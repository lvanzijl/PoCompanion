namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BacklogHealthSimplificationDocumentTests
{
    [TestMethod]
    public void BacklogHealthSimplification_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "backlog-health-simplification.md");

        Assert.IsTrue(File.Exists(reportPath), "The backlog health simplification report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Backlog Health Simplification");
        StringAssert.Contains(report, "## Removed Compatibility Wrapper");
        StringAssert.Contains(report, "## CDC BacklogQuality Usage");
        StringAssert.Contains(report, "## Handler Simplifications");
        StringAssert.Contains(report, "## Test Updates");
        StringAssert.Contains(report, "## Lines of Code Removed");
        StringAssert.Contains(report, "GetBacklogHealthQueryHandler");
        StringAssert.Contains(report, "GetMultiIterationBacklogHealthQueryHandler");
        StringAssert.Contains(report, "IBacklogQualityAnalysisService");
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
