namespace PoTool.Shared.WorkItems;

/// <summary>
/// Result of the validation queue query for a single category,
/// listing all affected rule groups sorted by item count descending.
/// Used by the Validation Queue page.
/// </summary>
/// <param name="CategoryKey">Short category key (e.g. "SI", "RR", "RC", "EFF").</param>
/// <param name="CategoryLabel">Human-readable label (e.g. "Structural Integrity").</param>
/// <param name="TotalItemCount">Total distinct work items affected in this category.</param>
/// <param name="RuleGroups">All rule groups sorted by item count descending.</param>
public sealed record ValidationQueueDto(
    string CategoryKey,
    string CategoryLabel,
    int TotalItemCount,
    List<ValidationQueueRuleGroupDto> RuleGroups
);

/// <summary>
/// A single rule group in the Validation Queue, aggregating affected work items for one rule.
/// </summary>
/// <param name="RuleId">The rule identifier (e.g. "SI-1", "RC-2").</param>
/// <param name="ShortTitle">Short human-readable description of the rule.</param>
/// <param name="ItemCount">Number of distinct work items that violate this rule.</param>
public sealed record ValidationQueueRuleGroupDto(
    string RuleId,
    string ShortTitle,
    int ItemCount
);
