using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// SI-2: A parent in Removed state with any descendant (recursive) not in Done or Removed is invalid.
/// </summary>
public sealed class RemovedParentWithUnfinishedDescendantsRule : HierarchicalValidationRuleBase
{
    private const string RemovedState = "Removed";

    /// <inheritdoc />
    public override string RuleId => "SI-2";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.StructuralIntegrity;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.BacklogHealthProblem;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.Process;

    /// <inheritdoc />
    protected override string MessageTemplate => "Parent in Removed state has unfinished descendants.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all items in Removed state
        var removedItems = itemsList.Where(w =>
            string.Equals(w.State, RemovedState, StringComparison.OrdinalIgnoreCase));

        foreach (var removedItem in removedItems)
        {
            var descendants = GetAllDescendants(removedItem.TfsId, itemsList);
            var unfinishedDescendants = descendants
                .Where(d => !IsFinishedState(d.State))
                .ToList();

            if (unfinishedDescendants.Count > 0)
            {
                var context = $"Unfinished: {string.Join(", ", unfinishedDescendants.Select(d => $"#{d.TfsId} ({d.State})"))}";
                results.Add(CreateViolation(removedItem.TfsId, context));
            }
        }

        return results;
    }
}
