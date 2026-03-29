using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Configuration;

namespace PoTool.Tests.Unit.Configuration;

[TestClass]
public class DataSourceModeConfigurationTests
{
    [TestMethod]
    public void GetRouteIntent_PullRequestsRoute_IsCacheOnlyAnalytical()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/pullrequests/123/comments");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.CacheOnlyAnalyticalRead,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_StartupDiscoveryRoute_IsLiveAllowed()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/startup/tfs-teams");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_ExplicitLiveWorkItemDiscoveryRoute_WinsOverCachePrefix()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/area-paths-from-tfs");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }
}
