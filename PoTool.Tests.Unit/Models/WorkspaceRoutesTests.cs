using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Models;

namespace PoTool.Tests.Unit.Models;

[TestClass]
public class WorkspaceRoutesTests
{
    [TestMethod]
    public void GetProductRoadmapEditor_ReturnsProductSpecificRoute()
    {
        var result = WorkspaceRoutes.GetProductRoadmapEditor(42);

        Assert.AreEqual("/planning/product-roadmaps/42", result);
    }
}
