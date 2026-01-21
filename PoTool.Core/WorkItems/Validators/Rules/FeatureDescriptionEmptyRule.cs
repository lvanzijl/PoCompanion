using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RR-2: Feature description is empty → invalid.
/// Ensures intent and context exist before refinement proceeds.
/// </summary>
public sealed class FeatureDescriptionEmptyRule : HierarchicalValidationRuleBase
{
    public FeatureDescriptionEmptyRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc />
    public override string RuleId => "RR-2";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.RefinementReadiness;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.RefinementBlocker;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.ProductOwner;

    /// <inheritdoc />
    protected override string MessageTemplate => "Feature description is empty.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all Features with empty descriptions
        var features = itemsList.Where(w =>
            string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase));

        foreach (var feature in features)
        {
            // Skip items in terminal states
            if (IsFinishedState(feature.Type, feature.State))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(feature.Description))
            {
                results.Add(CreateViolation(feature.TfsId));
            }
        }

        return results;
    }
}
