using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for parsing TFS work item fields from JSON payload.
/// Provides helper methods to extract specific fields like Severity, Tags, etc.
/// </summary>
public class TfsFieldParserService
{
    private readonly ILogger<TfsFieldParserService> _logger;

    public TfsFieldParserService(ILogger<TfsFieldParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts the Severity field from a work item's JSON payload.
    /// Severity is commonly used for bugs: "1 - Critical", "2 - High", "3 - Medium", "4 - Low".
    /// </summary>
    /// <param name="workItem">The work item to parse.</param>
    /// <returns>Severity value as string, or null if not found.</returns>
    public string? GetSeverity(WorkItemWithValidationDto workItem)
    {
        if (string.IsNullOrEmpty(workItem.JsonPayload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(workItem.JsonPayload);
            if (doc.RootElement.TryGetProperty("Microsoft.VSTS.Common.Severity", out var severity))
            {
                return severity.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parsing errors
        }

        return null;
    }

    /// <summary>
    /// Extracts tags from a work item.
    /// Prefers the cached Tags field for better performance, falls back to JSON payload if needed.
    /// Tags in TFS are stored as a semicolon-separated string in System.Tags.
    /// </summary>
    /// <param name="workItem">The work item to parse.</param>
    /// <returns>List of tag strings, empty list if none found.</returns>
    public List<string> GetTags(WorkItemWithValidationDto workItem)
    {
        // First, try to use the cached Tags field (faster and more reliable)
        if (!string.IsNullOrWhiteSpace(workItem.Tags))
        {
            // TFS tags are semicolon-separated
            return workItem.Tags
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
        }

        // Fallback: Parse from JSON payload if cached field is not available
        if (string.IsNullOrEmpty(workItem.JsonPayload))
        {
            return new List<string>();
        }

        try
        {
            using var doc = JsonDocument.Parse(workItem.JsonPayload);
            if (doc.RootElement.TryGetProperty("System.Tags", out var tags))
            {
                var tagsString = tags.GetString();
                if (!string.IsNullOrWhiteSpace(tagsString))
                {
                    // TFS tags are semicolon-separated
                    return tagsString
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parsing errors
        }

        return new List<string>();
    }

    /// <summary>
    /// Gets severity for a bug using the typed Severity property.
    /// NO FALLBACKS - returns null if Severity is not present or is invalid.
    /// Logs errors when data is missing or invalid.
    /// </summary>
    /// <param name="workItem">The bug work item.</param>
    /// <returns>Raw severity string from TFS, or null if not found.</returns>
    public string? GetBugSeverity(WorkItemWithValidationDto workItem)
    {
        // Use typed Severity property directly instead of parsing JsonPayload
        var severity = workItem.Severity;
        
        if (string.IsNullOrEmpty(severity))
        {
            _logger.LogError(
                "Work item {WorkItemId} is MISSING Severity field.",
                workItem.TfsId);
            return null;
        }
        
        // Return raw severity value from TFS - no normalization
        return severity;
    }
}
