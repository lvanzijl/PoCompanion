namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Interface for work item validation logic.
/// </summary>
public interface IWorkItemValidator
{
    /// <summary>
    /// Validates work items and returns validation issues for each item.
    /// </summary>
    /// <param name="workItems">All work items to validate.</param>
    /// <returns>Dictionary mapping TfsId to list of validation issues.</returns>
    Dictionary<int, List<ValidationIssue>> ValidateWorkItems(IEnumerable<WorkItemDto> workItems);
}
