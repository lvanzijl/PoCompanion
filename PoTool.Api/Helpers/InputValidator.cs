namespace PoTool.Api.Helpers;

/// <summary>
/// Provides input validation and sanitization for API parameters to prevent injection attacks.
/// </summary>
public static class InputValidator
{
    private const int MaxFilterLength = 200;
    private static readonly char[] DisallowedChars = new[] { '<', '>', ';', '"', '\'' };

    /// <summary>
    /// Sanitizes a filter string by removing dangerous characters and limiting length.
    /// </summary>
    /// <param name="input">Input filter string.</param>
    /// <returns>Sanitized filter string.</returns>
    public static string SanitizeFilter(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Trim and limit length
        var sanitized = input.Trim();
        if (sanitized.Length > MaxFilterLength)
            sanitized = sanitized.Substring(0, MaxFilterLength);

        // Remove potentially dangerous characters
        sanitized = new string(sanitized.Where(c => !DisallowedChars.Contains(c)).ToArray());

        return sanitized;
    }

    /// <summary>
    /// Validates that an area path contains only allowed characters.
    /// </summary>
    /// <param name="areaPath">Area path to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidAreaPath(string? areaPath)
    {
        if (string.IsNullOrWhiteSpace(areaPath))
            return false;

        // Area paths should only contain alphanumeric, backslash, space, dash, underscore
        return System.Text.RegularExpressions.Regex.IsMatch(
            areaPath, 
            @"^[a-zA-Z0-9\\\s\-_]+$"
        );
    }
}
