using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// SI-1: A parent in Done state with any descendant (recursive) not in Done or Removed is invalid.
/// </summary>
public sealed class DoneParentWithUnfinishedDescendantsRule : HierarchicalValidationRuleBase
{
    public DoneParentWithUnfinishedDescendantsRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc />
    public override string RuleId => "SI-1";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.StructuralIntegrity;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.BacklogHealthProblem;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.Process;

    /// <inheritdoc />
    protected override string MessageTemplate => "Parent in Done state has unfinished descendants.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all items in Done state (using state classification)
        var doneItems = itemsList.Where(w =>
        {
            var classification = StateClassificationService!.GetClassificationAsync(w.Type, w.State).GetAwaiter().GetResult();
            return classification == Shared.Settings.StateClassification.Done;
        });

        foreach (var doneItem in doneItems)
        {
            var descendants = GetAllDescendants(doneItem.TfsId, itemsList);
            var unfinishedDescendants = descendants
                .Where(d => !IsFinishedState(d.Type, d.State))
                .ToList();

            if (unfinishedDescendants.Count > 0)
            {
                var context = $"Unfinished: {string.Join(", ", unfinishedDescendants.Select(d => $"#{d.TfsId} ({d.State})"))}";
                results.Add(CreateViolation(doneItem.TfsId, context));
            }
        }

        return results;
    }
}
