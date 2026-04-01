using PoTool.Client.Helpers;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class WorkspaceQueryContextHelperTests
{
    [TestMethod]
    public void Parse_ReadsCanonicalWorkspaceIdentifiers()
    {
        var context = WorkspaceQueryContextHelper.Parse(
            "http://localhost:5292/home/pipeline-insights?productId=12&teamId=7&sprintId=31&fromSprintId=29&toSprintId=31");

        Assert.AreEqual(12, context.ProductId);
        Assert.AreEqual(7, context.TeamId);
        Assert.AreEqual(31, context.SprintId);
        Assert.AreEqual(29, context.FromSprintId);
        Assert.AreEqual(31, context.ToSprintId);
    }

    [TestMethod]
    public void BuildQueryString_PreservesKnownContextAndAdditionalParameters()
    {
        var queryString = WorkspaceQueryContextHelper.BuildQueryString(
            new WorkspaceQueryContext(ProductId: 5, TeamId: 3, SprintId: 9, FromSprintId: 7, ToSprintId: 9),
            "category=SI");

        Assert.AreEqual("?productId=5&teamId=3&sprintId=9&fromSprintId=7&toSprintId=9&category=SI", queryString);
    }

    [TestMethod]
    public void BuildRoute_UsesEmptyQueryForEmptyContext()
    {
        var route = WorkspaceQueryContextHelper.BuildRoute("/home/planning", new WorkspaceQueryContext());

        Assert.AreEqual("/home/planning", route);
    }
}
