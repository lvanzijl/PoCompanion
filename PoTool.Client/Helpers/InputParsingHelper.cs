namespace PoTool.Client.Helpers;

/// <summary>
/// Utility class for parsing comma-separated string inputs.
/// </summary>
[Obsolete("UNUSED: No references found in codebase. Confirmed via reachability analysis (Phase 1). See docs/reports/2026-03-30-cleanup-phase1-client-reachability-report.md section 6.2", error: true)]
public static class InputParsingHelper
{
    /// <summary>
    /// Parses a comma-separated string into a list of trimmed strings.
    /// </summary>
    /// <param name="input">The comma-separated input string.</param>
    /// <returns>A list of non-empty strings.</returns>
    public static List<string> ParseCommaSeparatedStrings(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new List<string>();
        }

        return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    /// <summary>
    /// Parses a comma-separated string into a list of integers.
    /// </summary>
    /// <param name="input">The comma-separated input string containing integers.</param>
    /// <returns>A list of parsed integers.</returns>
    public static List<int> ParseCommaSeparatedInts(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new List<int>();
        }

        return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();
    }
}
