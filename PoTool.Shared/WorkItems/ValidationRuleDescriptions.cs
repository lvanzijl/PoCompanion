namespace PoTool.Shared.WorkItems;

/// <summary>
/// Short human-readable descriptions for each known validation rule ID.
/// Used on the Validation Queue and Fix Session pages.
/// Keyed by rule ID (e.g. "SI-1").
/// </summary>
public static class ValidationRuleDescriptions
{
    /// <summary>
    /// Returns the short description for a rule ID, or the rule ID itself if not found.
    /// </summary>
    public static string GetTitle(string ruleId)
        => KnownTitles.TryGetValue(ruleId, out var title) ? title : ruleId;

    /// <summary>
    /// Returns the human-readable label for a category key, or the key itself if not found.
    /// </summary>
    public static string GetCategoryLabel(string categoryKey)
        => KnownCategoryLabels.TryGetValue(categoryKey, out var label) ? label : categoryKey;

    /// <summary>
    /// Mapping of all known rule IDs to their short display titles.
    /// Derived from the MessageTemplate properties on each rule class.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownTitles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Structural Integrity
            ["SI-1"] = "Parent in Done state has unfinished descendants",
            ["SI-2"] = "Parent in Removed state has non-Removed descendants",
            ["SI-3"] = "Parent in New state has InProgress or Done descendants",

            // Refinement Readiness
            ["RR-1"] = "Epic description is empty or too short",
            ["RR-2"] = "Feature description is empty or too short",
            ["RR-3"] = "Epic must have at least one Feature child",

            // Refinement Completeness
            ["RC-1"] = "PBI description is empty",
            ["RC-2"] = "Work item must have effort estimate",
            ["RC-3"] = "Feature has no children (PBIs)",
        };

    /// <summary>
    /// Mapping of category keys to their human-readable labels.
    /// Single source of truth used by both API handlers and client helpers.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownCategoryLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SI"]  = "Structural Integrity",
            ["RR"]  = "Refinement Readiness",
            ["RC"]  = "Refinement Completeness",
            ["EFF"] = "Missing Effort",
        };
}
