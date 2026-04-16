using System.Web;

namespace PoTool.Client.Services;

/// <summary>
/// Resolves authoritative startup state into concrete client URIs and ready-route intent.
/// </summary>
public static class StartupNavigationTargetResolver
{
    private static readonly HashSet<string> StartupFlowPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/profiles",
        "/sync-gate",
        "/startup-blocked"
    };

    public static string ResolveRequestedReadyUri(string? currentRelativeUri)
    {
        var relativeUri = string.IsNullOrWhiteSpace(currentRelativeUri)
            ? "/"
            : currentRelativeUri;
        var absoluteUri = new Uri($"https://startup.local/{relativeUri.TrimStart('/')}");
        var currentPath = StartupGuardRouteMatcher.NormalizePath(absoluteUri.PathAndQuery);

        if (StartupFlowPaths.Contains(currentPath))
        {
            var query = HttpUtility.ParseQueryString(absoluteUri.Query);
            if (StartupReturnUrlHelper.TryNormalize(query["returnUrl"], out var returnUrl))
            {
                return returnUrl;
            }

            return currentPath.Equals("/profiles", StringComparison.OrdinalIgnoreCase)
                ? "/profiles"
                : "/home";
        }

        if (currentPath == "/")
        {
            return "/home";
        }

        return StartupReturnUrlHelper.NormalizeOrDefault(absoluteUri.PathAndQuery);
    }

    public static string GetTargetUri(
        StartupResolutionState state,
        string requestedReadyUri,
        string message,
        string recoveryHint)
    {
        var normalizedReadyUri = StartupReturnUrlHelper.NormalizeOrDefault(requestedReadyUri);
        return state switch
        {
            StartupResolutionState.NoProfile or StartupResolutionState.ProfileInvalid
                => $"/profiles?returnUrl={Uri.EscapeDataString(normalizedReadyUri)}",
            StartupResolutionState.ProfileValid_NoSync
                => $"/sync-gate?returnUrl={Uri.EscapeDataString(normalizedReadyUri)}",
            StartupResolutionState.Ready => normalizedReadyUri,
            StartupResolutionState.Blocked => BuildBlockingUri(message, recoveryHint),
            _ => BuildBlockingUri(
                "Startup routing reached an unknown destination.",
                "Retry startup after confirming the backend is available.")
        };
    }

    public static bool IsCurrentTarget(string? currentRelativeUri, string targetUri)
    {
        var current = NormalizeUriForComparison(currentRelativeUri);
        var target = NormalizeUriForComparison(targetUri);
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUriForComparison(string? relativeUri)
    {
        if (string.IsNullOrWhiteSpace(relativeUri))
        {
            return "/";
        }

        var absoluteUri = new Uri($"https://startup.local/{relativeUri.TrimStart('/')}");
        var path = StartupGuardRouteMatcher.NormalizePath(absoluteUri.AbsolutePath);
        return string.IsNullOrEmpty(absoluteUri.Query)
            ? path
            : $"{path}{absoluteUri.Query}";
    }

    private static string BuildBlockingUri(string message, string recoveryHint)
    {
        message = Uri.EscapeDataString(message);
        var hint = Uri.EscapeDataString(recoveryHint);
        return $"/startup-blocked?message={message}&hint={hint}";
    }
}
