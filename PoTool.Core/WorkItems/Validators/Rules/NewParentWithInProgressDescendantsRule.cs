using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// SI-3: A parent in New state with any descendant in In Progress is invalid.
/// </summary>
public sealed class NewParentWithInProgressDescendantsRule : HierarchicalValidationRuleBase
{
    public NewParentWithInProgressDescendantsRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

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

        // Find all items in New state (using state classification)
        var newItems = itemsList.Where(w =>
        {
            var classification = StateClassificationService!.GetClassificationAsync(w.Type, w.State).GetAwaiter().GetResult();
            return classification == Shared.Settings.StateClassification.New;
        });

        foreach (var newItem in newItems)
        {
            var descendants = GetAllDescendants(newItem.TfsId, itemsList);
            var inProgressDescendants = descendants
                .Where(d =>
                {
                    var classification = StateClassificationService!.GetClassificationAsync(d.Type, d.State).GetAwaiter().GetResult();
                    return classification == Shared.Settings.StateClassification.InProgress;
                })
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
