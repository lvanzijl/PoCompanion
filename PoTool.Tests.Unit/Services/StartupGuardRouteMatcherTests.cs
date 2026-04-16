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
    public void IsCurrentTarget_MatchingRoute_ReturnsTrue()
    {
        var isCurrentTarget = StartupNavigationTargetResolver.IsCurrentTarget(
            "/sync-gate?returnUrl=%2Fhome",
            "/sync-gate?returnUrl=%2Fhome");

        Assert.IsTrue(isCurrentTarget);
    }

    [TestMethod]
    public void IsCurrentTarget_DifferentQuery_ReturnsFalse()
    {
        var isCurrentTarget = StartupNavigationTargetResolver.IsCurrentTarget(
            "/profiles?returnUrl=%2Fhome",
            "/profiles?returnUrl=%2Fhome%2Fdelivery");

        Assert.IsFalse(isCurrentTarget);
    }

    [TestMethod]
    public void GetBlockingRoute_ReturnsStartupBlocked()
    {
        Assert.AreEqual("/startup-blocked", StartupNavigationTargetResolver.GetBlockingRoute());
    }
}
