using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Interface for the hierarchical work item validator that enforces
/// evaluation order and suppression rules between validation categories.
/// </summary>
public interface IHierarchicalWorkItemValidator
{
    /// <summary>
    /// Validates a collection of work items using hierarchical validation rules.
    /// </summary>
    /// <param name="workItems">The work items to validate.</param>
    /// <returns>
    /// A collection of validation results, one per work item tree.
    /// Each result contains categorized violations with suppression applied.
    /// </returns>
    IReadOnlyList<HierarchicalValidationResult> ValidateWorkItems(IEnumerable<WorkItemDto> workItems);

    /// <summary>
    /// Validates a single work item tree starting from a root work item.
    /// </summary>
    /// <param name="rootWorkItemId">The TFS ID of the root work item.</param>
    /// <param name="workItems">All work items (must include the root and its descendants).</param>
    /// <returns>
    /// The validation result for the tree rooted at the specified work item.
    /// </returns>
    HierarchicalValidationResult ValidateTree(int rootWorkItemId, IEnumerable<WorkItemDto> workItems);
}
