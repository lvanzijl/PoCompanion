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
    public void GetProductRoadmapEditor_ReturnsProductSpecificRoute()
    {
        var result = WorkspaceRoutes.GetProductRoadmapEditor(42);

        Assert.AreEqual("/planning/product-roadmaps/42", result);
    }
}
