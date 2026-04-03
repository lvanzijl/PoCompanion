namespace PoTool.Client.Services;

/// <summary>
/// Matches startup-guard route exemptions without allowing the root route to become a wildcard.
/// </summary>
public static class StartupGuardRouteMatcher
{
    public static bool IsExemptPath(string currentPath, IEnumerable<string> exemptPaths)
    {
        var normalizedCurrentPath = NormalizePath(currentPath);

        foreach (var exemptPath in exemptPaths.Select(NormalizePath))
        {
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
