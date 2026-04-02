using PoTool.Shared.WorkItems;

namespace PoTool.Client.Helpers;

public static class ValidationRouteContextHelper
{
    public static bool TryNormalizeCategory(string? rawCategory, out string categoryKey)
    {
        categoryKey = string.Empty;

        if (string.IsNullOrWhiteSpace(rawCategory))
        {
            return false;
        }

        var normalizedCategory = rawCategory.Trim().ToUpperInvariant();
        if (!ValidationRuleCatalog.KnownCategoryLabels.ContainsKey(normalizedCategory))
        {
            return false;
        }

        categoryKey = normalizedCategory;
        return true;
    }

    public static bool TryNormalizeRuleForCategory(string? rawRuleId, string categoryKey, out string ruleId)
    {
        ruleId = string.Empty;

        if (string.IsNullOrWhiteSpace(rawRuleId))
        {
            return false;
        }

        var normalizedRuleId = rawRuleId.Trim().ToUpperInvariant();
        if (!ValidationRuleCatalog.KnownRules.ContainsKey(normalizedRuleId))
        {
            return false;
        }

        if (!ValidationRuleCatalog.MatchesUiCategory(normalizedRuleId, categoryKey))
        {
            return false;
        }

        ruleId = normalizedRuleId;
        return true;
    }
}
