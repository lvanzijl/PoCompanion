using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RC-2: Epic, Feature, or PBI effort is empty → invalid.
/// Assesses whether work items have effort estimates for planning.
/// Only evaluated if all Refinement Readiness rules pass.
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
    protected override string MessageTemplate => "Work item effort is empty.";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all Epics, Features, and PBIs
        var targetItems = itemsList.Where(w =>
            string.Equals(w.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase));

        foreach (var item in targetItems)
        {
            // Skip items in terminal states
            if (IsFinishedState(item.Type, item.State))
            {
                continue;
            }

            // Check if item has no effort
            if (!item.Effort.HasValue || item.Effort.Value == 0)
            {
                results.Add(CreateViolation(item.TfsId));
            }
        }

        return results;
    }
}
