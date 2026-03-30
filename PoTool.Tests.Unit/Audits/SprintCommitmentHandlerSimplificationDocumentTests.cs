namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SprintCommitmentHandlerSimplificationDocumentTests
{
    [TestMethod]
    public void SprintCommitmentHandlerSimplification_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "sprint_commitment_handler_simplification.md");

        Assert.IsTrue(File.Exists(reportPath), "The sprint commitment handler simplification report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Sprint Commitment Handler Simplification");
        StringAssert.Contains(report, "## Removed Handler Calculations");
        StringAssert.Contains(report, "## New CDC SprintFactResult");
        StringAssert.Contains(report, "## Updated Handlers");
        StringAssert.Contains(report, "## DTO Simplification");
        StringAssert.Contains(report, "## Test Adjustments");
        StringAssert.Contains(report, "## Lines of Code Removed");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandler.cs");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandler.cs");
        StringAssert.Contains(report, "SprintFactResult.cs");
        StringAssert.Contains(report, "ISprintFactService");
        StringAssert.Contains(report, "RemainingStoryPoints");
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
