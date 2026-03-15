namespace PoTool.Shared.WorkItems;

/// <summary>
/// Canonical validation rule metadata used by adapters and UI consumers.
/// </summary>
public static class ValidationRuleCatalog
{
    public static readonly IReadOnlyDictionary<string, ValidationRuleDescriptor> KnownRules =
        new Dictionary<string, ValidationRuleDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["SI-1"] = new("SI-1", "Parent in Done state has unfinished descendants", "SI", "SI", ValidationCategory.StructuralIntegrity),
            ["SI-2"] = new("SI-2", "Parent in Removed state has non-Removed descendants", "SI", "SI", ValidationCategory.StructuralIntegrity),
            ["SI-3"] = new("SI-3", "Parent in New state has InProgress or Done descendants", "SI", "SI", ValidationCategory.StructuralIntegrity),
            ["RR-1"] = new("RR-1", "Epic description is empty or too short", "RR", "RR", ValidationCategory.RefinementReadiness),
            ["RR-2"] = new("RR-2", "Feature description is empty or too short", "RR", "RR", ValidationCategory.RefinementReadiness),
            ["RR-3"] = new("RR-3", "Epic must have at least one Feature child", "RR", "RR", ValidationCategory.RefinementReadiness),
            ["RC-1"] = new("RC-1", "PBI description is empty", "RC", "RC", ValidationCategory.RefinementCompleteness),
            ["RC-2"] = new("RC-2", "Work item must have effort estimate", "RC", "EFF", ValidationCategory.MissingEffort),
            ["RC-3"] = new("RC-3", "Feature has no children (PBIs)", "RC", "RC", ValidationCategory.RefinementCompleteness),
        };

    public static readonly IReadOnlyDictionary<string, string> KnownCategoryLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SI"] = "Structural Integrity",
            ["RR"] = "Refinement Readiness",
            ["RC"] = "Refinement Completeness",
            ["EFF"] = "Missing Effort",
        };

    public static bool TryGetRule(string ruleId, out ValidationRuleDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        return KnownRules.TryGetValue(ruleId, out descriptor!);
    }

    public static string GetTitle(string ruleId)
        => TryGetRule(ruleId, out var descriptor) ? descriptor.Title : ruleId;

    public static string GetCategoryLabel(string categoryKey)
        => KnownCategoryLabels.TryGetValue(categoryKey, out var label) ? label : categoryKey;

    public static string? GetFamilyKey(string ruleId)
        => TryGetRule(ruleId, out var descriptor) ? descriptor.FamilyKey : null;

    public static string? GetUiCategoryKey(string ruleId)
        => TryGetRule(ruleId, out var descriptor) ? descriptor.UiCategoryKey : null;

    public static ValidationCategory? GetCategory(string ruleId)
        => TryGetRule(ruleId, out var descriptor) ? descriptor.Category : null;

    public static bool MatchesUiCategory(string ruleId, string categoryKey)
    {
        if (!TryGetRule(ruleId, out var descriptor))
        {
            return false;
        }

        return string.Equals(descriptor.UiCategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Canonical metadata projection for a validation rule.
/// </summary>
/// <param name="RuleId">Canonical rule identifier (e.g. "RC-2").</param>
/// <param name="Title">Short human-readable rule title.</param>
/// <param name="FamilyKey">Canonical rule family key (e.g. "RC").</param>
/// <param name="UiCategoryKey">UI category key after adapter aliases are applied (e.g. "EFF").</param>
/// <param name="Category">Legacy/shared validation category.</param>
public sealed record ValidationRuleDescriptor(
    string RuleId,
    string Title,
    string FamilyKey,
    string UiCategoryKey,
    ValidationCategory Category
);
