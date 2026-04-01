using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Models;

namespace PoTool.Tests.Unit.Models;

[TestClass]
public class WorkspaceRoutesTests
{
    [TestMethod]
    public void HomeChanges_ReturnsExpectedRoute()
    {
        var route = typeof(WorkspaceRoutes)
            .GetField(nameof(WorkspaceRoutes.HomeChanges))?
            .GetRawConstantValue() as string;

        Assert.AreEqual("/home/changes", route);
    }

    [TestMethod]
    public void HealthOverview_ReturnsExpectedRoute()
    {
        var route = typeof(WorkspaceRoutes)
            .GetField(nameof(WorkspaceRoutes.HealthOverview))?
            .GetRawConstantValue() as string;

        Assert.AreEqual("/home/health/overview", route);
    }

    [TestMethod]
    public void BacklogOverview_ReturnsExpectedCanonicalRoute()
    {
        var route = typeof(WorkspaceRoutes)
            .GetField(nameof(WorkspaceRoutes.BacklogOverview))?
            .GetRawConstantValue() as string;

        Assert.AreEqual("/home/health/backlog-health", route);
    }

    [TestMethod]
    public void BacklogOverviewLegacy_ReturnsExpectedLegacyRoute()
    {
        var route = typeof(WorkspaceRoutes)
            .GetField(nameof(WorkspaceRoutes.BacklogOverviewLegacy))?
            .GetRawConstantValue() as string;

        Assert.AreEqual("/home/backlog-overview", route);
    }

    [TestMethod]
    public void GetProductRoadmapEditor_ReturnsProductSpecificRoute()
    {
        var result = WorkspaceRoutes.GetProductRoadmapEditor(42);

        Assert.AreEqual("/planning/product-roadmaps/42", result);
    }

    [TestMethod]
    public void GetProjectProductRoadmaps_ReturnsAliasScopedRoute()
    {
        var result = WorkspaceRoutes.GetProjectProductRoadmaps("payments-platform");

        Assert.AreEqual("/planning/payments-platform/product-roadmaps", result);
    }

    [TestMethod]
    public void GetProjectPlanBoard_ReturnsAliasScopedRoute()
    {
        var result = WorkspaceRoutes.GetProjectPlanBoard("payments-platform");

        Assert.AreEqual("/planning/payments-platform/plan-board", result);
    }

    [TestMethod]
    public void GetProjectPlanningOverview_ReturnsAliasScopedRoute()
    {
        var result = WorkspaceRoutes.GetProjectPlanningOverview("payments-platform");

        Assert.AreEqual("/planning/payments-platform/overview", result);
    }
}
