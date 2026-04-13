using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WiqlQueryBuilderTests
{
    [TestMethod]
    public void BuildWorkItemsQuery_MinimalQuery_IsValid()
    {
        var query = WiqlQueryBuilder.BuildWorkItemsQuery(["[System.Id]"]);

        Assert.AreEqual("SELECT [System.Id] FROM WorkItems", query);
    }

    [TestMethod]
    public void BuildWorkItemsQuery_OptionalFilters_ComposesDeterministically()
    {
        var query = WiqlQueryBuilder.BuildWorkItemsQuery(
            selectFields: ["[System.Id]"],
            whereClauses:
            [
                "[System.AreaPath] UNDER 'Project\\Area'",
                "[System.ChangedDate] >= '2026-04-13T20:32:37.2010000Z'"
            ],
            orderByClauses: ["[System.Id] DESC"]);

        Assert.AreEqual(
            "SELECT [System.Id] FROM WorkItems WHERE [System.AreaPath] UNDER 'Project\\Area' AND [System.ChangedDate] >= '2026-04-13T20:32:37.2010000Z' ORDER BY [System.Id] DESC",
            query);
    }

    [TestMethod]
    public void BuildWorkItemsQuery_EmptySelectField_Throws()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(() => WiqlQueryBuilder.BuildWorkItemsQuery([" "]));

        StringAssert.Contains(ex.Message, "select field");
    }

    [TestMethod]
    public void BuildWorkItemsQuery_EmptyWhereClause_Throws()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(() => WiqlQueryBuilder.BuildWorkItemsQuery(
            selectFields: ["[System.Id]"],
            whereClauses: [" "]));

        StringAssert.Contains(ex.Message, "WHERE clause");
    }

    [TestMethod]
    public void Validate_UnsupportedTop_Throws()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(() => WiqlQueryBuilder.Validate(
            "SELECT TOP 5 [System.Id] FROM WorkItems ORDER BY [System.Id] DESC"));

        StringAssert.Contains(ex.Message, "SELECT TOP");
    }

    [TestMethod]
    public void Validate_EmptyInFilter_Throws()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(() => WiqlQueryBuilder.Validate(
            "SELECT [System.Id] FROM WorkItemLinks WHERE ([Source].[System.Id] IN ()) MODE (Recursive)"));

        StringAssert.Contains(ex.Message, "empty IN");
    }
}
