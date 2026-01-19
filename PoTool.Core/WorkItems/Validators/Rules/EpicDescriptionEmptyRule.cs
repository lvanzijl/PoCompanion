using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators.Rules;

/// <summary>
/// RR-1: Epic description is empty → invalid.
/// Ensures intent and context exist before refinement proceeds.
/// </summary>
public sealed class EpicDescriptionEmptyRule : HierarchicalValidationRuleBase
{
    /// <inheritdoc />
    public override string RuleId => "RR-1";

    /// <inheritdoc />
    public override ValidationCategory Category => ValidationCategory.RefinementReadiness;

    /// <inheritdoc />
    public override ValidationConsequence Consequence => ValidationConsequence.RefinementBlocker;

    /// <inheritdoc />
    public override ResponsibleParty ResponsibleParty => ResponsibleParty.ProductOwner;

    /// <inheritdoc />
    protected override string MessageTemplate => "Epic description is empty.";

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
            if (IsFinishedState(epic.State))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(epic.Description))
            {
                results.Add(CreateViolation(epic.TfsId));
            }
        }

        return results;
    }
}
