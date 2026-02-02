using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// SI-2: A parent in Removed state with any descendant (recursive) not in Removed is invalid.
/// Only descendants in Removed are allowed.
/// </summary>
public sealed class RemovedParentWithUnfinishedDescendantsRule : HierarchicalValidationRuleBase
{
    public RemovedParentWithUnfinishedDescendantsRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc />
    public override string RuleId => "SI-2";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.StructuralIntegrity;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.BacklogHealthProblem;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.Process;

    /// <inheritdoc />
    protected override string MessageTemplate => "Parent in Removed state has non-Removed descendants.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all items in Removed state (using state classification)
        var removedItems = itemsList.Where(w =>
        {
            var classification = StateClassificationService!.GetClassificationAsync(w.Type, w.State).GetAwaiter().GetResult();
            return classification == Shared.Settings.StateClassification.Removed;
        });

        foreach (var removedItem in removedItems)
        {
            var descendants = GetAllDescendants(removedItem.TfsId, itemsList);
            var nonRemovedDescendants = descendants
                .Where(d =>
                {
                    var classification = StateClassificationService!.GetClassificationAsync(d.Type, d.State).GetAwaiter().GetResult();
                    return classification != Shared.Settings.StateClassification.Removed;
                })
                .ToList();

            if (nonRemovedDescendants.Count > 0)
            {
                var context = $"Non-Removed: {string.Join(", ", nonRemovedDescendants.Select(d => $"#{d.TfsId} ({d.State})"))}";
                results.Add(CreateViolation(removedItem.TfsId, context));
            }
        }

        return results;
    }
}
