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
        => ValidationRuleCatalog.GetTitle(ruleId);

    /// <summary>
    /// Returns the human-readable label for a category key, or the key itself if not found.
    /// </summary>
    public static string GetCategoryLabel(string categoryKey)
        => ValidationRuleCatalog.GetCategoryLabel(categoryKey);

    /// <summary>
    /// Mapping of all known rule IDs to their short display titles.
    /// Derived from the MessageTemplate properties on each rule class.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownTitles =
        ValidationRuleCatalog.KnownRules.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Title,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Mapping of category keys to their human-readable labels.
    /// Single source of truth used by both API handlers and client helpers.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownCategoryLabels =
        ValidationRuleCatalog.KnownCategoryLabels;
}
