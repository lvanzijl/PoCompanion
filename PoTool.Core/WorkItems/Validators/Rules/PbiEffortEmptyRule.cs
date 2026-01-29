using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RC-2: PBI effort is empty → invalid.
/// Assesses whether PBIs are ready for implementation.
/// Only evaluated if all Refinement Readiness rules pass.
/// Suppressed if parent Feature is a Refinement Blocker (invalid description).
/// </summary>
public sealed class PbiEffortEmptyRule : HierarchicalValidationRuleBase
{
    public PbiEffortEmptyRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }
    /// <inheritdoc />
    public override string RuleId => "RC-2";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.RefinementCompleteness;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.IncompleteRefinement;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.DevelopmentTeam;

    /// <inheritdoc />
    protected override string MessageTemplate => "PBI effort is empty.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all PBIs (Product Backlog Items) without effort
        var pbis = itemsList.Where(w =>
            string.Equals(w.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase));

        foreach (var pbi in pbis)
        {
            // Skip items in terminal states
            if (IsFinishedState(pbi.Type, pbi.State))
            {
                continue;
            }

            // Check if PBI has no effort
            if (!pbi.Effort.HasValue || pbi.Effort.Value == 0)
            {
                // Suppression rule: Skip if parent Feature is a Refinement Blocker
                if (pbi.ParentTfsId.HasValue)
                {
                    var parentFeature = itemsList.FirstOrDefault(w =>
                        w.TfsId == pbi.ParentTfsId.Value &&
                        string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase));

                    // If parent is a Feature with invalid description (refinement blocker), suppress this PBI's validation
                    if (parentFeature != null &&
                        (string.IsNullOrWhiteSpace(parentFeature.Description) || parentFeature.Description.Length < ValidationRuleConstants.MinimumDescriptionLength))
                    {
                        continue;
                    }
                }

                results.Add(CreateViolation(pbi.TfsId));
            }
        }

        return results;
    }
}
