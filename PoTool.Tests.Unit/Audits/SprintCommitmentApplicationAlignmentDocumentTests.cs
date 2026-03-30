namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SprintCommitmentApplicationAlignmentDocumentTests
{
    [TestMethod]
    public void SprintCommitmentApplicationAlignment_ReportExistsWithMigrationSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "sprint-commitment-application-alignment.md");

        Assert.IsTrue(File.Exists(reportPath), "The sprint commitment application alignment audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Sprint Commitment Application Alignment");
        StringAssert.Contains(report, "## Legacy application-layer usages located");
        StringAssert.Contains(report, "## Handlers migrated");
        StringAssert.Contains(report, "## Legacy helper references removed");
        StringAssert.Contains(report, "## Tests updated");
        StringAssert.Contains(report, "## Remaining migration risks");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandler");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandler");
        StringAssert.Contains(report, "SprintTrendProjectionService");
        StringAssert.Contains(report, "PortfolioFlowProjectionService");
        StringAssert.Contains(report, "ISprintCommitmentService");
        StringAssert.Contains(report, "ISprintScopeChangeService");
        StringAssert.Contains(report, "ISprintCompletionService");
        StringAssert.Contains(report, "ISprintSpilloverService");
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
