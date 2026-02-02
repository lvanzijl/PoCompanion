using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RR-3: Epic without children (Features) → Refinement Needed.
/// Ensures that an Epic has at least one Feature before refinement can proceed.
/// </summary>
public sealed class EpicWithoutFeaturesRule : HierarchicalValidationRuleBase
{
    public EpicWithoutFeaturesRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc />
    public override string RuleId => "RR-3";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.RefinementReadiness;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.RefinementBlocker;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.ProductOwner;

    /// <inheritdoc />
    protected override string MessageTemplate => "Epic has no children (Features).";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all Epics
        var epics = itemsList.Where(w =>
            string.Equals(w.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase));

        foreach (var epic in epics)
        {
            // Skip items in terminal states
            if (IsFinishedState(epic.Type, epic.State))
            {
                continue;
            }

            // Check if Epic has any children (Features)
            var hasChildren = itemsList.Any(w => w.ParentTfsId == epic.TfsId &&
                string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase));

            if (!hasChildren)
            {
                results.Add(CreateViolation(epic.TfsId));
            }
        }

        return results;
    }
}
