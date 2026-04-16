namespace PoTool.Client.Services;

/// <summary>
/// Client-side startup navigation helpers for comparing the current route with the authoritative target route.
/// </summary>
public static class StartupNavigationTargetResolver
{
    public static bool IsCurrentTarget(string? currentRelativeUri, string targetUri)
    {
        var current = NormalizeUriForComparison(currentRelativeUri);
        var target = NormalizeUriForComparison(targetUri);
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetBlockingRoute()
    {
        return "/startup-blocked";
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
}
