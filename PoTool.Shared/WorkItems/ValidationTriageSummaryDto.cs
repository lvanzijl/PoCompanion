namespace PoTool.Shared.WorkItems;

/// <summary>
/// Summary of validation issues grouped by category for the Validation Triage page.
/// </summary>
/// <param name="StructuralIntegrity">SI category triage data.</param>
/// <param name="RefinementReadiness">RR category triage data.</param>
/// <param name="RefinementCompleteness">RC category triage data.</param>
/// <param name="MissingEffort">EFF category triage data (items missing effort estimates).</param>
public sealed record ValidationTriageSummaryDto(
    ValidationCategoryTriageDto StructuralIntegrity,
    ValidationCategoryTriageDto RefinementReadiness,
    ValidationCategoryTriageDto RefinementCompleteness,
    ValidationCategoryTriageDto MissingEffort
);

/// <summary>
/// Triage data for a single validation category.
/// </summary>
/// <param name="CategoryKey">Short category key shown in URLs and filters (e.g. "SI", "RR", "RC", "EFF").</param>
/// <param name="CategoryLabel">Human-readable label (e.g. "Structural Integrity").</param>
/// <param name="TotalItemCount">Total number of distinct work items affected in this category.</param>
/// <param name="TopRuleGroups">Top rule groups sorted by item count descending (up to 3).</param>
public sealed record ValidationCategoryTriageDto(
    string CategoryKey,
    string CategoryLabel,
    int TotalItemCount,
    List<ValidationRuleGroupDto> TopRuleGroups
);

/// <summary>
/// A rule group within a validation category, aggregating the number of affected work items.
/// </summary>
/// <param name="RuleId">The rule identifier (e.g. "SI-1", "RC-2").</param>
/// <param name="ItemCount">Number of distinct work items that violate this rule.</param>
public sealed record ValidationRuleGroupDto(
    string RuleId,
    int ItemCount
);
