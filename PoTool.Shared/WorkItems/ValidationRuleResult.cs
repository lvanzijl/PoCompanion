namespace PoTool.Shared.WorkItems;

/// <summary>
/// Represents the result of evaluating a validation rule against a specific work item.
/// </summary>
/// <param name="Rule">The validation rule that was evaluated.</param>
/// <param name="WorkItemId">The TFS ID of the work item that was validated.</param>
/// <param name="IsViolated">True if the rule was violated; false if the rule passed.</param>
/// <param name="AdditionalContext">Optional additional context about the violation.</param>
public sealed record ValidationRuleResult(
    ValidationRule Rule,
    int WorkItemId,
    bool IsViolated,
    string? AdditionalContext = null
);
