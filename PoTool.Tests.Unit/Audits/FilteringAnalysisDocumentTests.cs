namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class FilteringAnalysisDocumentTests
{
    [TestMethod]
    public void FilteringAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "filtering.md");

        Assert.IsTrue(File.Exists(reportPath), "The filtering analysis report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Global Filters & Context Model Analysis");
        StringAssert.Contains(report, "## 1. Current filter architecture");
        StringAssert.Contains(report, "## 2. Filter dimensions");
        StringAssert.Contains(report, "## 3. Conflicts with desired global model");
        StringAssert.Contains(report, "## 4. Required refactor points");
        StringAssert.Contains(report, "## 5. Summary");
        StringAssert.Contains(report, "WorkspaceBase");
        StringAssert.Contains(report, "INavigationContextService");
        StringAssert.Contains(report, "ParseContextQueryParameters");
        StringAssert.Contains(report, "BuildContextQuery");
        StringAssert.Contains(report, "IProfileService");
        StringAssert.Contains(report, "productId");
        StringAssert.Contains(report, "teamId");
        StringAssert.Contains(report, "sprintId");
        StringAssert.Contains(report, "GlobalFilterService");
        StringAssert.Contains(report, "ProductOwnerId");
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
