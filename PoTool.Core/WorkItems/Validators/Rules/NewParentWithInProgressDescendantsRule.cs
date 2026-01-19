using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// SI-3: A parent in New state with any descendant in In Progress is invalid.
/// </summary>
public sealed class NewParentWithInProgressDescendantsRule : HierarchicalValidationRuleBase
{
    private const string NewState = "New";
    private const string InProgressState = "In Progress";

    /// <inheritdoc />
    public override string RuleId => "SI-3";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.StructuralIntegrity;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.BacklogHealthProblem;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.Process;

    /// <inheritdoc />
    protected override string MessageTemplate => "Parent in New state has In Progress descendants.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all items in New state
        var newItems = itemsList.Where(w =>
            string.Equals(w.State, NewState, StringComparison.OrdinalIgnoreCase));

        foreach (var newItem in newItems)
        {
            var descendants = GetAllDescendants(newItem.TfsId, itemsList);
            var inProgressDescendants = descendants
                .Where(d => string.Equals(d.State, InProgressState, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (inProgressDescendants.Count > 0)
            {
                var context = $"In Progress: {string.Join(", ", inProgressDescendants.Select(d => $"#{d.TfsId}"))}";
                results.Add(CreateViolation(newItem.TfsId, context));
            }
        }

        return results;
    }
}
