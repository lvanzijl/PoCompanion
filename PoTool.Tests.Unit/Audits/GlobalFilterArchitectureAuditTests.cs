namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class GlobalFilterArchitectureAuditTests
{
    private static readonly string[] ManagedFilterPageFiles =
    [
        "PoTool.Client/Pages/Home/BacklogOverviewPage.razor",
        "PoTool.Client/Pages/Home/BugOverview.razor",
        "PoTool.Client/Pages/Home/DeliveryTrends.razor",
        "PoTool.Client/Pages/Home/HealthOverviewPage.razor",
        "PoTool.Client/Pages/Home/HomePage.razor",
        "PoTool.Client/Pages/Home/PipelineInsights.razor",
        "PoTool.Client/Pages/Home/PlanBoard.razor",
        "PoTool.Client/Pages/Home/PortfolioDelivery.razor",
        "PoTool.Client/Pages/Home/PortfolioProgressPage.razor",
        "PoTool.Client/Pages/Home/PrDeliveryInsights.razor",
        "PoTool.Client/Pages/Home/PrOverview.razor",
        "PoTool.Client/Pages/Home/ProductRoadmapEditor.razor",
        "PoTool.Client/Pages/Home/ProductRoadmaps.razor",
        "PoTool.Client/Pages/Home/ProjectPlanningOverview.razor",
        "PoTool.Client/Pages/Home/SprintExecution.razor",
        "PoTool.Client/Pages/Home/SprintTrend.razor",
        "PoTool.Client/Pages/Home/SprintTrendActivity.razor",
        "PoTool.Client/Pages/Home/TrendsWorkspace.razor",
        "PoTool.Client/Pages/Home/ValidationFixPage.razor",
        "PoTool.Client/Pages/Home/ValidationQueuePage.razor",
        "PoTool.Client/Pages/Home/ValidationTriagePage.razor"
    ];

    private static readonly string[] ManagedFilterQueryKeys =
    [
        "\"teamId\"",
        "\"sprintId\"",
        "\"productId\"",
        "\"projectId\"",
        "\"projectAlias\"",
        "\"fromSprintId\"",
        "\"toSprintId\"",
        "\"rollingWindow\"",
        "\"rollingUnit\"",
        "\"timeMode\""
    ];

    [TestMethod]
    public void ManagedFilterPages_UseGlobalFilterStoreOrWorkspaceBase()
    {
        var repositoryRoot = GetRepositoryRoot();

        foreach (var relativePath in ManagedFilterPageFiles)
        {
            var path = Path.Combine(repositoryRoot, relativePath);
            var content = File.ReadAllText(path);
            var usesSharedFilterInfrastructure =
                content.Contains("GlobalFilterStore", StringComparison.Ordinal)
                || content.Contains("@inherits WorkspaceBase", StringComparison.Ordinal);

            Assert.IsTrue(
                usesSharedFilterInfrastructure,
                $"Managed filter page '{relativePath}' must use GlobalFilterStore directly or inherit WorkspaceBase.");
        }
    }

    [TestMethod]
    public void HomePages_DoNotParseManagedFilterQueryKeysLocally()
    {
        var repositoryRoot = GetRepositoryRoot();
        var pagesDirectory = Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home");
        var pageFiles = Directory.GetFiles(pagesDirectory, "*.razor", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var pagePath in pageFiles)
        {
            var content = File.ReadAllText(pagePath);
            if (!content.Contains("HttpUtility.ParseQueryString", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var queryKey in ManagedFilterQueryKeys)
            {
                Assert.IsFalse(
                    content.Contains(queryKey, StringComparison.Ordinal),
                    $"Page '{pagePath}' must not parse shared filter query key {queryKey} directly.");
            }
        }
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
