using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for parsing TFS work item fields from JSON payload.
/// Provides helper methods to extract specific fields like Priority, Severity, Tags, etc.
/// </summary>
public class TfsFieldParserService
{
    private readonly ILogger<TfsFieldParserService> _logger;

    public TfsFieldParserService(ILogger<TfsFieldParserService> logger)
    {
        _logger = logger;
    }
    /// <summary>
    /// Extracts the Priority field from a work item's JSON payload.
    /// Priority in TFS is typically: 1, 2, 3, 4 (where 1 is highest priority).
    /// </summary>
    /// <param name="workItem">The work item to parse.</param>
    /// <returns>Priority value as string, or null if not found.</returns>
    public string? GetPriority(WorkItemWithValidationDto workItem)
    {
        if (string.IsNullOrEmpty(workItem.JsonPayload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(workItem.JsonPayload);
            var root = doc.RootElement;

            // Try Microsoft.VSTS.Common.Priority first (standard for bugs)
            if (root.TryGetProperty("Microsoft.VSTS.Common.Priority", out var priority))
            {
                return priority.GetInt32().ToString();
            }

            // Fallback: try System.Priority (some TFS configurations)
            if (root.TryGetProperty("System.Priority", out var sysPriority))
            {
                return sysPriority.GetInt32().ToString();
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parsing errors
        }

        return null;
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
    /// Extracts tags from a work item's JSON payload.
    /// Tags in TFS are stored as a semicolon-separated string in System.Tags.
    /// </summary>
    /// <param name="workItem">The work item to parse.</param>
    /// <returns>List of tag strings, empty list if none found.</returns>
    public List<string> GetTags(WorkItemWithValidationDto workItem)
    {
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
    /// Gets severity for a bug by checking ONLY the Severity field.
    /// NO FALLBACKS - returns null if Severity is not present or is invalid.
    /// Logs errors when data is missing or invalid.
    /// </summary>
    /// <param name="workItem">The bug work item.</param>
    /// <returns>Raw severity string from TFS, or null if not found.</returns>
    public string? GetBugSeverity(WorkItemWithValidationDto workItem)
    {
        // Get severity - NO FALLBACKS
        var severity = GetSeverity(workItem);
        
        if (string.IsNullOrEmpty(severity))
        {
            // Log error with available field information
            var availableFields = GetAvailableFieldNames(workItem);
            _logger.LogError(
                "Work item {WorkItemId} is MISSING Severity field (Microsoft.VSTS.Common.Severity). " +
                "Available fields: {AvailableFields}",
                workItem.TfsId,
                string.Join(", ", availableFields.Take(20)));
            return null;
        }
        
        // Return raw severity value from TFS - no normalization
        return severity;
    }
    
    /// <summary>
    /// Gets list of available field names from work item JSON payload.
    /// Used for error reporting when expected fields are missing.
    /// </summary>
    private List<string> GetAvailableFieldNames(WorkItemWithValidationDto workItem)
    {
        if (string.IsNullOrEmpty(workItem.JsonPayload))
        {
            return new List<string> { "(JsonPayload is empty)" };
        }

        try
        {
            using var doc = JsonDocument.Parse(workItem.JsonPayload);
            return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        }
        catch (JsonException)
        {
            return new List<string> { "(Failed to parse JsonPayload)" };
        }
    }
}
