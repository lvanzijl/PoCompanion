using System.Text.Json;
using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for parsing TFS work item fields from JSON payload.
/// Provides helper methods to extract specific fields like Priority, Severity, Tags, etc.
/// </summary>
public class TfsFieldParserService
{
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
    /// Maps TFS Priority (1-4) to Bug Severity levels.
    /// Priority 1 = Critical, Priority 2 = High, Priority 3 = Medium, Priority 4 = Low.
    /// </summary>
    /// <param name="priority">Priority string from TFS (e.g., "1", "2", "3", "4").</param>
    /// <returns>Severity string matching BugSeverity constants.</returns>
    public string MapPriorityToSeverity(string? priority)
    {
        return priority switch
        {
            "1" => Models.BugSeverity.Critical,
            "2" => Models.BugSeverity.High,
            "3" => Models.BugSeverity.Medium,
            "4" => Models.BugSeverity.Low,
            _ => Models.BugSeverity.Medium // Default to Medium if unknown
        };
    }

    /// <summary>
    /// Normalizes TFS Severity strings to standard Bug Severity levels.
    /// Severity format: "1 - Critical", "2 - High", "3 - Medium", "4 - Low" or just "Critical", "High", etc.
    /// </summary>
    /// <param name="severity">Severity string from TFS.</param>
    /// <returns>Severity string matching BugSeverity constants.</returns>
    public string NormalizeSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return Models.BugSeverity.Medium;
        }

        // Handle both "1 - Critical" format and simple "Critical" format
        var severityLower = severity.ToLowerInvariant();
        
        if (severityLower.Contains("critical") || severityLower.StartsWith("1"))
        {
            return Models.BugSeverity.Critical;
        }
        if (severityLower.Contains("high") || severityLower.StartsWith("2"))
        {
            return Models.BugSeverity.High;
        }
        if (severityLower.Contains("medium") || severityLower.StartsWith("3"))
        {
            return Models.BugSeverity.Medium;
        }
        if (severityLower.Contains("low") || severityLower.StartsWith("4"))
        {
            return Models.BugSeverity.Low;
        }

        return Models.BugSeverity.Medium; // Default to Medium if unknown
    }

    /// <summary>
    /// Maps Bug Severity levels back to TFS Priority values (1-4).
    /// Critical = 1, High = 2, Medium = 3, Low = 4.
    /// </summary>
    /// <param name="severity">Severity string from BugSeverity constants.</param>
    /// <returns>Priority value as int (1-4).</returns>
    public int MapSeverityToPriority(string severity)
    {
        return severity switch
        {
            Models.BugSeverity.Critical => 1,
            Models.BugSeverity.High => 2,
            Models.BugSeverity.Medium => 3,
            Models.BugSeverity.Low => 4,
            _ => 3 // Default to Medium (3) if unknown
        };
    }

    /// <summary>
    /// Gets severity for a bug by checking the Severity field.
    /// Falls back to Priority field if Severity is not available.
    /// </summary>
    /// <param name="workItem">The bug work item.</param>
    /// <returns>Severity string matching BugSeverity constants.</returns>
    public string GetBugSeverity(WorkItemWithValidationDto workItem)
    {
        // Try Severity first (this is what we should be using for bugs)
        var severity = GetSeverity(workItem);
        if (!string.IsNullOrEmpty(severity))
        {
            return NormalizeSeverity(severity);
        }

        // Fallback to Priority if Severity not available
        var priority = GetPriority(workItem);
        if (!string.IsNullOrEmpty(priority))
        {
            return MapPriorityToSeverity(priority);
        }

        // Default to Medium if neither field is available
        return Models.BugSeverity.Medium;
    }
}
