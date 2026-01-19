using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Interface for individual hierarchical validation rules.
/// Each rule belongs to a specific category and produces typed results.
/// </summary>
public interface IHierarchicalValidationRule
{
    /// <summary>
    /// Gets the unique identifier for this rule (e.g., "SI-1", "RR-1").
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Gets the validation category this rule belongs to.
    /// </summary>
    ValidationCategory Category { get; }

    /// <summary>
    /// Gets the consequence when this rule is violated.
    /// </summary>
    ValidationConsequence Consequence { get; }

    /// <summary>
    /// Gets the party responsible for addressing violations.
    /// </summary>
    ResponsibleParty ResponsibleParty { get; }

    /// <summary>
    /// Evaluates the rule against a set of work items.
    /// </summary>
    /// <param name="workItems">All work items to evaluate.</param>
    /// <returns>A list of rule results, one for each violated item.</returns>
    IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems);
}
