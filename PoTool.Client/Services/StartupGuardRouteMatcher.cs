namespace PoTool.Client.Services;

/// <summary>
/// Matches startup-guard route exemptions without allowing the root route to become a wildcard.
/// </summary>
public static class StartupGuardRouteMatcher
{
    /// <summary>
    /// Returns true when the current path is exempt from startup gating.
    /// The root route matches exactly; all other exempt routes may match the exact path or nested subpaths.
    /// </summary>
    public static bool IsExemptPath(string currentPath, IEnumerable<string> exemptPaths)
    {
        var normalizedCurrentPath = NormalizePath(currentPath);

        foreach (var rawExemptPath in exemptPaths)
        {
            var exemptPath = NormalizePath(rawExemptPath);
            if (exemptPath == "/")
            {
                if (normalizedCurrentPath == "/")
                {
                    return true;
                }

                continue;
            }

            if (normalizedCurrentPath.Equals(exemptPath, StringComparison.OrdinalIgnoreCase)
                || normalizedCurrentPath.StartsWith($"{exemptPath}/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Normalizes a route path by removing query and fragment parts, ensuring a leading slash,
    /// and trimming trailing slashes except for the root path.
    /// </summary>
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalizedPath = path.Split('?', '#')[0].Trim();
        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = $"/{normalizedPath}";
        }

        return normalizedPath.Length > 1
            ? normalizedPath.TrimEnd('/')
            : normalizedPath;
    }
}
