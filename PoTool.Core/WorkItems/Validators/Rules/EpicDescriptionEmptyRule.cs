using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RR-1: Epic description is empty or too short → invalid.
/// Ensures intent and context exist before refinement proceeds.
/// Description must be at least 10 characters long.
/// </summary>
public sealed class EpicDescriptionEmptyRule : HierarchicalValidationRuleBase
{
    public EpicDescriptionEmptyRule(IWorkItemStateClassificationService stateClassificationService)
    {
        StateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc />
    public override string RuleId => "RR-1";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.RefinementReadiness;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.RefinementBlocker;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.ProductOwner;

    /// <inheritdoc />
    protected override string MessageTemplate => "Epic description is empty or too short (must be at least 10 characters).";

    /// <inheritdoc />
    public override IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

        // Find all Epics with empty descriptions
        var epics = itemsList.Where(w =>
            string.Equals(w.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase));

        foreach (var epic in epics)
        {
            // Skip items in terminal states
            if (IsFinishedState(epic.Type, epic.State))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(epic.Description) || epic.Description.Length < 10)
            {
                results.Add(CreateViolation(epic.TfsId));
            }
        }

        return results;
    }
}
