using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class StartupGuardRouteMatcherTests
{
    [TestMethod]
    public void IsExemptPath_RootExemption_MatchesOnlyRoot()
    {
        var exemptPaths = new[] { "/" };

        Assert.IsTrue(StartupGuardRouteMatcher.IsExemptPath("/", exemptPaths));
        Assert.IsFalse(StartupGuardRouteMatcher.IsExemptPath("/home", exemptPaths));
    }

    [TestMethod]
    public void IsExemptPath_SettingsPrefix_MatchesNestedSettingsRoutes()
    {
        var exemptPaths = new[] { "/settings" };

        Assert.IsTrue(StartupGuardRouteMatcher.IsExemptPath("/settings", exemptPaths));
        Assert.IsTrue(StartupGuardRouteMatcher.IsExemptPath("/settings/tfs", exemptPaths));
    }

    [TestMethod]
    public void GetTargetUri_SyncRoute_UsesSyncGate()
    {
        var route = new StartupRoutingResult(
            StartupRoute.SyncGate,
            "Sync required",
            "Open sync gate.",
            IsBlocking: false);

        var target = StartupNavigationTargetResolver.GetTargetUri(route);

        Assert.AreEqual("/sync-gate?returnUrl=%2Fhome", target);
    }

    [TestMethod]
    public void GetTargetUri_BlockingRoute_UsesStartupBlockedPage()
    {
        var route = new StartupRoutingResult(
            StartupRoute.BlockingError,
            "Startup failed",
            "Retry startup.",
            IsBlocking: true);

        var target = StartupNavigationTargetResolver.GetTargetUri(route);

        StringAssert.StartsWith(target, "/startup-blocked?message=");
        StringAssert.Contains(target, "hint=");
    }
}
