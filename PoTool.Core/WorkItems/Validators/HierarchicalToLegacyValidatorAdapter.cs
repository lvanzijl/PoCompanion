using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Adapts <see cref="IHierarchicalWorkItemValidator"/> to the <see cref="IWorkItemValidator"/> interface,
/// so hierarchical rule violations (SI, RR, RC) appear in the legacy validation pipeline
/// and are visible in the Validation Queue and Fix Session pages.
///
/// Severity mapping:
///   BacklogHealthProblem (SI rules)  → "Error"
///   RefinementBlocker    (RR rules)  → "Warning"
///   IncompleteRefinement (RC rules)  → "Warning"
/// </summary>
public sealed class HierarchicalToLegacyValidatorAdapter : IWorkItemValidator
{
    private readonly IHierarchicalWorkItemValidator _validator;

    public HierarchicalToLegacyValidatorAdapter(IHierarchicalWorkItemValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc />
    public Dictionary<int, List<ValidationIssue>> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var result = new Dictionary<int, List<ValidationIssue>>();
        var hierarchicalResults = _validator.ValidateWorkItems(workItems);

        foreach (var treeResult in hierarchicalResults)
        {
            foreach (var violation in treeResult.AllViolations)
            {
                var severity = violation.Rule.Consequence == ValidationConsequence.BacklogHealthProblem
                    ? "Error"
                    : "Warning";

                if (!result.TryGetValue(violation.WorkItemId, out var issues))
                {
                    issues = new List<ValidationIssue>();
                    result[violation.WorkItemId] = issues;
                }

                issues.Add(new ValidationIssue(severity, violation.Rule.Message, violation.Rule.RuleId));
            }
        }

        return result;
    }
}
