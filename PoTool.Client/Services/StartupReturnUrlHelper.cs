namespace PoTool.Client.Services;

public static class StartupReturnUrlHelper
{
    public static string NormalizeOrDefault(string? returnUrl, string fallback = "/home")
    {
        return TryNormalize(returnUrl, out var normalized)
            ? normalized
            : fallback;
    }

    public static bool TryNormalize(string? returnUrl, out string normalizedReturnUrl)
    {
        normalizedReturnUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        var candidate = returnUrl.Trim();
        if (!candidate.StartsWith("/", StringComparison.Ordinal)
            || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.IndexOf("//", 2, StringComparison.Ordinal) >= 0
            || candidate.Contains('\\')
            || candidate.Contains(':')
            || !Uri.TryCreate(candidate, UriKind.Relative, out _))
        {
            return false;
        }

        normalizedReturnUrl = candidate;
        return true;
    }
}
