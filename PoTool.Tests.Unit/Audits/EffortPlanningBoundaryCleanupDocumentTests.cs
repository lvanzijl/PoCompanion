namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class EffortPlanningBoundaryCleanupDocumentTests
{
    [TestMethod]
    public void EffortPlanningBoundaryCleanup_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "effort-planning-boundary-cleanup.md");

        Assert.IsTrue(File.Exists(reportPath), "The effort planning boundary cleanup report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# EffortPlanning Boundary Cleanup");
        StringAssert.Contains(report, "## CDC Text Formatting Removed");
        StringAssert.Contains(report, "## Structured Suggestion Facts Added");
        StringAssert.Contains(report, "## API Adapter Formatting Added");
        StringAssert.Contains(report, "## Tests Updated");
        StringAssert.Contains(report, "## Final Boundary Status");
        StringAssert.Contains(report, "EffortEstimationSuggestionService.cs");
        StringAssert.Contains(report, "EffortEstimationSuggestionMapper.cs");
        StringAssert.Contains(report, "GetEffortEstimationSuggestionsQueryHandler.cs");
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
