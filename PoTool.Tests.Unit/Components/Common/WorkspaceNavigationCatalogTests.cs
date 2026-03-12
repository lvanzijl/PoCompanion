using System.Linq;
using PoTool.Client.Components.Common;
using PoTool.Client.Models;

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
    [DataRow("Health", WorkspaceRoutes.ValidationTriage, true)]
    [DataRow("Health", WorkspaceRoutes.BacklogOverview, true)]
    [DataRow("Health", WorkspaceRoutes.BugDetail, true)]
    [DataRow("Delivery", WorkspaceRoutes.SprintDelivery, true)]
    [DataRow("Delivery", WorkspaceRoutes.SprintExecution, true)]
    [DataRow("Delivery", WorkspaceRoutes.SprintTrendActivity, true)]
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
}
