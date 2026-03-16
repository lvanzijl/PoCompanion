namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ApplicationSimplificationPlanDocumentTests
{
    [TestMethod]
    public void ApplicationSimplificationPlan_ReportExistsWithRequiredSectionsAndContent()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "roadmaps", "application_simplification_plan.md");

        Assert.IsTrue(File.Exists(reportPath), "The application simplification plan should exist under docs/roadmaps.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Application Simplification Plan");
        StringAssert.Contains(report, "## Refactor Groups");
        StringAssert.Contains(report, "## Handler Migration Plan");
        StringAssert.Contains(report, "## Service Removal Candidates");
        StringAssert.Contains(report, "## Calculator Consolidation");
        StringAssert.Contains(report, "## Expected Codebase Impact");
        StringAssert.Contains(report, "## Risk Notes");

        StringAssert.Contains(report, "BacklogQuality");
        StringAssert.Contains(report, "SprintCommitment");
        StringAssert.Contains(report, "DeliveryTrends");
        StringAssert.Contains(report, "Forecasting");
        StringAssert.Contains(report, "EffortDiagnostics");
        StringAssert.Contains(report, "PortfolioFlow");

        StringAssert.Contains(report, "Handler");
        StringAssert.Contains(report, "→ Load required work-item data");
        StringAssert.Contains(report, "→ Call CDC slice");
        StringAssert.Contains(report, "→ Map canonical result to DTO");
        StringAssert.Contains(report, "→ Return response");

        StringAssert.Contains(report, "Handlers must not:");
        StringAssert.Contains(report, "calculate analytics");
        StringAssert.Contains(report, "reconstruct sprint history");
        StringAssert.Contains(report, "compute rollups");
        StringAssert.Contains(report, "compute velocity or flow metrics");

        StringAssert.Contains(report, "Step 1: redirect calculation to the `SprintCommitment` slice");
        StringAssert.Contains(report, "Step 2: remove duplicated service logic");
        StringAssert.Contains(report, "Step 3: simplify DTO builders");
        StringAssert.Contains(report, "Step 4: remove unused helper utilities");

        StringAssert.Contains(report, "RoadmapAnalyticsService.cs");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandler.cs");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "SprintExecutionDtos.cs");
        StringAssert.Contains(report, "DeliveryTrendProgressRollupMapper.cs");
        StringAssert.Contains(report, "150–250 lines");
        StringAssert.Contains(report, "GetEffortDistributionQueryHandler");
        StringAssert.Contains(report, "GetEffortEstimationQualityQueryHandler");
        StringAssert.Contains(report, "GetEffortEstimationSuggestionsQueryHandler");
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
