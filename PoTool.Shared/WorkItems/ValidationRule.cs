namespace PoTool.Shared.WorkItems;

/// <summary>
/// Represents a validation rule that can be applied to work items.
/// </summary>
/// <param name="RuleId">Unique identifier for the rule (e.g., "SI-1", "RR-1", "RC-1").</param>
/// <param name="Category">The validation category this rule belongs to.</param>
/// <param name="Consequence">The consequence type when this rule is violated.</param>
/// <param name="ResponsibleParty">The party responsible for addressing violations of this rule.</param>
/// <param name="Message">Human-readable description of the violation.</param>
public sealed record ValidationRule(
    string RuleId,
    ValidationCategory Category,
    ValidationConsequence Consequence,
    ResponsibleParty ResponsibleParty,
    string Message
);
