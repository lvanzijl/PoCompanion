namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BugTrendTfsDashboardMismatchDocumentTests
{
    [TestMethod]
    public void BugTrendTfsDashboardMismatch_ReportExistsWithRootCauseAndIssueComments()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = $"{repositoryRoot}/docs/audits/bug_trend_tfs_dashboard_mismatch.md";

        Assert.IsTrue(File.Exists(reportPath), "The bug trend mismatch report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Bug Trend TFS vs Dashboard Mismatch");
        StringAssert.Contains(report, "## ROOT_CAUSE");
        StringAssert.Contains(report, "## Comments on the Issue (you are @copilot in this section)");
        StringAssert.Contains(report, "PoTool.Client/Pages/Home/DeliveryTrends.razor");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Services/SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Api/Services/ActivityEventIngestionService.cs");
        StringAssert.Contains(report, "PoTool.Api/Services/WorkItemResolutionService.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs");
        StringAssert.Contains(report, "aggregation/query semantics mismatch");
        StringAssert.Contains(report, "The chart does not restrict closed bugs to the sprint backlog.");
        StringAssert.Contains(report, "`Resolved` and `Closed` are **not** default Done states for `Bug`");
        StringAssert.Contains(report, "`ClosedDate` is already ingested from TFS, but the sprint trend pipeline does not use it.");
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists($"{current.FullName}/PoTool.sln"))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
