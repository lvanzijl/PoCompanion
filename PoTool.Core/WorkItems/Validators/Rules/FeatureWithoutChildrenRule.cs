using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RC-3: Feature without children (PBIs) → Refinement Needed.
/// Only applies if the Feature itself has a valid description (not a refinement blocker).
/// </summary>
public sealed class FeatureWithoutChildrenRule : HierarchicalValidationRuleBase
{
    public FeatureWithoutChildrenRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc />
    public override string RuleId => "RC-3";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.RefinementCompleteness;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.IncompleteRefinement;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.ProductOwner;

    /// <inheritdoc />
    protected override string MessageTemplate => "Feature has no children (PBIs).";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all Features
        var features = itemsList.Where(w =>
            string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase));

        foreach (var feature in features)
        {
            // Skip items in terminal states
            if (IsFinishedState(feature.Type, feature.State))
            {
                continue;
            }

            // Skip if the Feature itself is a refinement blocker (invalid description)
            if (string.IsNullOrWhiteSpace(feature.Description) || feature.Description.Length < 10)
            {
                continue;
            }

            // Check if Feature has any children (PBIs)
            var hasChildren = itemsList.Any(w => w.ParentTfsId == feature.TfsId &&
                string.Equals(w.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase));

            if (!hasChildren)
            {
                results.Add(CreateViolation(feature.TfsId));
            }
        }

        return results;
    }
}
