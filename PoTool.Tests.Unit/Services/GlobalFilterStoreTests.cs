using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GlobalFilterStoreTests
{
    [TestMethod]
    public void TrackNavigation_UnknownRoute_ResetsToNeutral()
    {
        var store = CreateStore();

        store.TrackNavigation("http://localhost/settings");

        Assert.IsNull(store.CurrentUsage);
        Assert.IsTrue(store.CurrentState.AllProducts);
        Assert.IsTrue(store.CurrentState.AllProjects);
        Assert.AreEqual(GlobalFilterTimeMode.Snapshot, store.CurrentState.TimeMode);
    }

    [TestMethod]
    public void TrackNavigation_ProjectScopedPlanningRoute_UsesProjectAliasFromPath()
    {
        var store = CreateStore();

        store.TrackNavigation("http://localhost/planning/payments-platform/overview", 42);

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("ProjectPlanningOverview", store.CurrentUsage.PageName);
        CollectionAssert.AreEqual(new[] { "payments-platform" }, store.CurrentState.ProjectAliases.ToArray());
        Assert.AreEqual(42, store.CurrentUsage.ActiveProfileId);
    }

    [TestMethod]
    public void TrackNavigation_SprintRouteWithoutSelections_FlagsMissingTeamAndSprint()
    {
        var store = CreateStore();

        store.TrackNavigation("http://localhost/home/delivery/sprint");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("SprintTrend", store.CurrentUsage.PageName);
        Assert.IsTrue(store.CurrentUsage.MissingTeam);
        Assert.IsTrue(store.CurrentUsage.MissingSprint);
    }

    [TestMethod]
    public void TrackNavigation_QueryBackedRoute_TracksKnownFilterValues()
    {
        var store = CreateStore();

        store.TrackNavigation("http://localhost/home/trends?productId=5&teamId=7&fromSprintId=100&toSprintId=101");

        Assert.IsNotNull(store.CurrentUsage);
        CollectionAssert.AreEqual(new[] { 5 }, store.CurrentState.ProductIds.ToArray());
        Assert.AreEqual(7, store.CurrentState.TeamId);
        Assert.AreEqual(GlobalFilterTimeMode.Trend, store.CurrentState.TimeMode);
        Assert.AreEqual("from 100 → to 101", store.CurrentState.TimeValue);
        Assert.IsFalse(store.CurrentUsage.MissingTeam);
        Assert.IsFalse(store.CurrentUsage.MissingSprint);
    }

    private static GlobalFilterStore CreateStore()
        => new(NullLogger<GlobalFilterStore>.Instance);
}
