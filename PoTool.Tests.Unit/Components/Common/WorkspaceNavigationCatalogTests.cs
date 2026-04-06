using PoTool.Client.Components.Common;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Components.Common;

[TestClass]
public class WorkspaceNavigationCatalogTests
{
    [TestMethod]
    public void NormalizePath_StripsLeadingSlash_Query_AndFragment()
    {
        var result = WorkspaceNavigationCatalog.NormalizePath("/home/delivery/?team=42#section");

        Assert.AreEqual("home/delivery", result);
    }

    [TestMethod]
    [DataRow("Health", WorkspaceRoutes.HealthWorkspace, true)]
    [DataRow("Health", WorkspaceRoutes.HealthOverview, true)]
    [DataRow("Health", WorkspaceRoutes.ValidationTriage, true)]
    [DataRow("Health", WorkspaceRoutes.BacklogOverview, true)]
    [DataRow("Health", WorkspaceRoutes.BacklogOverviewLegacy, false)]
    [DataRow("Delivery", WorkspaceRoutes.SprintDelivery, true)]
    [DataRow("Delivery", WorkspaceRoutes.SprintExecution, true)]
    [DataRow("Delivery", WorkspaceRoutes.SprintTrend, false)]
    [DataRow("Delivery", WorkspaceRoutes.SprintTrendActivity, false)]
    [DataRow("Trends", WorkspaceRoutes.PrOverview, true)]
    [DataRow("Trends", WorkspaceRoutes.PipelineInsights, true)]
    [DataRow("Planning", WorkspaceRoutes.ProductRoadmaps, true)]
    [DataRow("Planning", WorkspaceRoutes.PlanBoard, true)]
    [DataRow("Planning", WorkspaceRoutes.DeliveryWorkspace, false)]
    [DataRow("Trends", WorkspaceRoutes.HomeChanges, false)]
    public void Items_ResolveWorkspaceActivity_ByRoute(string label, string route, bool expectedActive)
    {
        var item = WorkspaceNavigationCatalog.Items.Single(definition => definition.Label == label);

        var isActive = item.IsActive(route);

        Assert.AreEqual(expectedActive, isActive);
    }

    [TestMethod]
    public void GetVisibleItems_HidesOnboarding_WhenFeatureFlagDisabled()
    {
        var result = WorkspaceNavigationCatalog.GetVisibleItems(new TestFeatureFlagService(false));

        Assert.IsFalse(result.Any(item => item.Label == "Onboarding"));
    }

    [TestMethod]
    public void GetVisibleItems_ShowsOnboarding_WhenFeatureFlagEnabled()
    {
        var result = WorkspaceNavigationCatalog.GetVisibleItems(new TestFeatureFlagService(true));

        var item = result.Single(definition => definition.Label == "Onboarding");
        Assert.IsTrue(item.IsActive(WorkspaceRoutes.OnboardingWorkspace));
    }

    private sealed class TestFeatureFlagService : IFeatureFlagService
    {
        private readonly bool _enabled;

        public TestFeatureFlagService(bool enabled)
        {
            _enabled = enabled;
        }

        public bool IsEnabled(string key)
            => key == FeatureFlagKeys.OnboardingWorkspace && _enabled;
    }
}
