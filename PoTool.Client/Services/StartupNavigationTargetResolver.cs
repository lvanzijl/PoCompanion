namespace PoTool.Client.Services;

/// <summary>
/// Resolves root-startup routing decisions into concrete client URIs.
/// </summary>
public static class StartupNavigationTargetResolver
{
    /// <summary>
    /// Maps a startup routing decision to the concrete client URI that should be opened.
    /// </summary>
    public static string GetTargetUri(StartupRoutingResult routingResult, string? returnUrl = null)
    {
        return routingResult.Route switch
        {
            StartupRoute.Home => "/home",
            StartupRoute.Configuration => "/settings/tfs",
            StartupRoute.CreateFirstProfile or StartupRoute.ProfilesHome => BuildProfilesUri(returnUrl),
            StartupRoute.SyncGate => BuildSyncGateUri(returnUrl),
            StartupRoute.BlockingError => BuildBlockingUri(routingResult),
            _ => BuildBlockingUri(new StartupRoutingResult(
                StartupRoute.BlockingError,
                "Startup routing reached an unknown destination.",
                "Retry startup after confirming the backend is available.",
                IsBlocking: true))
        };
    }

    private static string BuildProfilesUri(string? returnUrl)
    {
        var normalizedReturnUrl = StartupReturnUrlHelper.NormalizeOrDefault(returnUrl);
        return $"/profiles?returnUrl={Uri.EscapeDataString(normalizedReturnUrl)}";
    }

    private static string BuildSyncGateUri(string? returnUrl)
    {
        var normalizedReturnUrl = StartupReturnUrlHelper.NormalizeOrDefault(returnUrl);
        return $"/sync-gate?returnUrl={Uri.EscapeDataString(normalizedReturnUrl)}";
    }

    private static string BuildBlockingUri(StartupRoutingResult routingResult)
    {
        var message = Uri.EscapeDataString(routingResult.Message);
        var hint = Uri.EscapeDataString(routingResult.RecoveryHint);
        return $"/startup-blocked?message={message}&hint={hint}";
    }
}
