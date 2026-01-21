using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Base class for hierarchical validation rules, providing common functionality.
/// </summary>
public abstract class HierarchicalValidationRuleBase : IHierarchicalValidationRule
{
    /// <summary>
    /// State classification service for checking work item state mappings.
    /// Optional - only set for rules that need it.
    /// </summary>
    protected IWorkItemStateClassificationService? StateClassificationService { get; set; }
    /// <inheritdoc />
    public abstract string RuleId { get; }

    /// <inheritdoc />
    public abstract ValidationCategory Category { get; }

    /// <inheritdoc />
    public abstract ValidationConsequence Consequence { get; }

    /// <inheritdoc />
    public abstract ResponsibleParty ResponsibleParty { get; }

    /// <summary>
    /// Gets the message template for violations of this rule.
    /// </summary>
    protected abstract string MessageTemplate { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<ValidationRuleResult> Evaluate(IEnumerable<WorkItemDto> workItems);

    /// <summary>
    /// Creates a validation rule definition for this rule.
    /// </summary>
    protected ValidationRule CreateRule(string message) =>
        new(RuleId, Category, Consequence, ResponsibleParty, message);

    /// <summary>
    /// Creates a violation result for a specific work item.
    /// </summary>
    protected ValidationRuleResult CreateViolation(int workItemId, string? additionalContext = null)
    {
        var message = additionalContext != null
            ? $"{MessageTemplate} {additionalContext}"
            : MessageTemplate;

        return new ValidationRuleResult(
            CreateRule(message),
            workItemId,
            IsViolated: true,
            additionalContext
        );
    }

    /// <summary>
    /// Determines if a work item state represents a finished state (Done or Removed).
    /// Uses state classification service.
    /// </summary>
    protected bool IsFinishedState(string workItemType, string state)
    {
        if (StateClassificationService == null)
        {
            throw new InvalidOperationException("StateClassificationService is required for state validation.");
        }
        
        var classification = StateClassificationService.GetClassificationAsync(workItemType, state).GetAwaiter().GetResult();
        return classification == Shared.Settings.StateClassification.Done || 
               classification == Shared.Settings.StateClassification.Removed;
    }

    /// <summary>
    /// Builds a lookup dictionary from TFS ID to work item.
    /// </summary>
    protected static Dictionary<int, WorkItemDto> BuildLookup(IEnumerable<WorkItemDto> workItems)
    {
        var list = workItems as List<WorkItemDto> ?? workItems.ToList();
        return list.ToDictionary(w => w.TfsId);
    }

    /// <summary>
    /// Gets all descendants of a work item recursively.
    /// </summary>
    protected static IEnumerable<WorkItemDto> GetAllDescendants(
        int parentId,
        IEnumerable<WorkItemDto> allItems)
    {
        var itemsList = allItems as List<WorkItemDto> ?? allItems.ToList();
        var directChildren = itemsList.Where(w => w.ParentTfsId == parentId).ToList();

        foreach (var child in directChildren)
        {
            yield return child;

            foreach (var descendant in GetAllDescendants(child.TfsId, itemsList))
            {
                yield return descendant;
            }
        }
    }
}
