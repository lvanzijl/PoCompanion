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
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/area-paths/from-tfs");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_WorkItemGoalsDiscoveryRoute_IsLiveAllowed()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/goals/from-tfs");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_WorkItemRevisionsRoute_IsLiveAllowed()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/123/revisions");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_WorkItemStateTimelineRoute_IsLiveAllowed()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/123/state-timeline");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.BlockedAmbiguous,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_WorkItemUpdateRoute_IsLiveAllowed()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/123/iteration-path");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_WorkItemValidationTriageRoute_RemainsCacheOnlyAnalytical()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/validation-triage");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.CacheOnlyAnalyticalRead,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_WorkItemStaticSupportRoute_IsLiveAllowed()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/workitems/bug-severity-options");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }

    [TestMethod]
    public void GetRouteIntent_PipelineDefinitionsDiscoveryRoute_WinsOverPipelinesCachePrefix()
    {
        var intent = DataSourceModeConfiguration.GetRouteIntent("/api/pipelines/definitions");

        Assert.AreEqual(
            DataSourceModeConfiguration.RouteIntent.LiveAllowed,
            intent);
    }

    [TestMethod]
    public void ResolveRouteIntentOrThrow_UnknownRoute_Throws()
    {
        try
        {
            DataSourceModeConfiguration.ResolveRouteIntentOrThrow("/api/unclassified-route");
            Assert.Fail("Expected RouteNotClassifiedException was not thrown.");
        }
        catch (PoTool.Api.Exceptions.RouteNotClassifiedException)
        {
            // Expected path
        }
    }
}
