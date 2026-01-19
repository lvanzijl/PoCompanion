using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RC-1: PBI description is empty → invalid.
/// Assesses whether PBIs are ready for implementation.
/// Only evaluated if all Refinement Readiness rules pass.
/// </summary>
public sealed class PbiDescriptionEmptyRule : HierarchicalValidationRuleBase
{
    /// <inheritdoc />
    public override string RuleId => "RC-1";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.RefinementCompleteness;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.IncompleteRefinement;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.DevelopmentTeam;

    /// <inheritdoc />
    protected override string MessageTemplate => "PBI description is empty.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all PBIs (Product Backlog Items) with empty descriptions
        var pbis = itemsList.Where(w =>
            string.Equals(w.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase));

        foreach (var pbi in pbis)
        {
            // Skip items in terminal states
            if (IsFinishedState(pbi.State))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(pbi.Description))
            {
                results.Add(CreateViolation(pbi.TfsId));
            }
        }

        return results;
    }
}
