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
        var target = StartupNavigationTargetResolver.GetTargetUri(
            StartupResolutionState.ProfileValid_NoSync,
            "/home",
            "Sync required",
            "Open sync gate.");

        Assert.AreEqual("/sync-gate?returnUrl=%2Fhome", target);
    }

    [TestMethod]
    public void GetTargetUri_ProfilesRoute_PreservesDeepLinkReturnUrl()
    {
        var target = StartupNavigationTargetResolver.GetTargetUri(
            StartupResolutionState.NoProfile,
            "/home/delivery/execution?sprintId=7",
            "Profile selection required",
            "Select a profile.");

        Assert.AreEqual("/profiles?returnUrl=%2Fhome%2Fdelivery%2Fexecution%3FsprintId%3D7", target);
    }

    [TestMethod]
    public void GetTargetUri_BlockingRoute_UsesStartupBlockedPage()
    {
        var target = StartupNavigationTargetResolver.GetTargetUri(
            StartupResolutionState.Blocked,
            "/home",
            "Startup failed",
            "Retry startup.");

        StringAssert.StartsWith(target, "/startup-blocked?message=");
        StringAssert.Contains(target, "hint=");
    }

    [TestMethod]
    public void ResolveRequestedReadyUri_StartupFlowRoute_UsesReturnUrl()
    {
        var requestedReadyUri = StartupNavigationTargetResolver.ResolveRequestedReadyUri("/sync-gate?returnUrl=%2Fhome%2Fdelivery");

        Assert.AreEqual("/home/delivery", requestedReadyUri);
    }

    [TestMethod]
    public void NormalizeReturnUrl_InvalidUrl_FallsBackToHome()
    {
        var normalized = StartupReturnUrlHelper.NormalizeOrDefault("//evil.example");

        Assert.AreEqual("/home", normalized);
    }
}
