namespace PoTool.Client.Services;

/// <summary>
/// Resolves root-startup routing decisions into concrete client URIs.
/// </summary>
public static class StartupNavigationTargetResolver
{
    /// <summary>
    /// Maps a startup routing decision to the concrete client URI that should be opened.
    /// </summary>
    public static string GetTargetUri(StartupRoutingResult routingResult)
    {
        return routingResult.Route switch
        {
            StartupRoute.Home => "/home",
            StartupRoute.Configuration => "/settings/tfs",
            StartupRoute.CreateFirstProfile or StartupRoute.ProfilesHome => "/profiles",
            StartupRoute.SyncGate => "/sync-gate?returnUrl=%2Fhome",
            StartupRoute.BlockingError => BuildBlockingUri(routingResult),
            _ => BuildBlockingUri(new StartupRoutingResult(
                StartupRoute.BlockingError,
                "Startup routing reached an unknown destination.",
                "Retry startup after confirming the backend is available.",
                IsBlocking: true))
        };
    }

    private static string BuildBlockingUri(StartupRoutingResult routingResult)
    {
        var message = Uri.EscapeDataString(routingResult.Message);
        var hint = Uri.EscapeDataString(routingResult.RecoveryHint);
        return $"/startup-blocked?message={message}&hint={hint}";
    }
}
