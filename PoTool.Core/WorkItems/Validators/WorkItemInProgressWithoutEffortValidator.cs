namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validates that work items in "In Progress" state have an effort estimate.
/// </summary>
public class WorkItemInProgressWithoutEffortValidator : IWorkItemValidator
{
    private const string InProgressState = "In Progress";
    private const string ErrorSeverity = "Error";

    /// <inheritdoc/>
    public Dictionary<int, List<ValidationIssue>> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var result = new Dictionary<int, List<ValidationIssue>>();

        foreach (var item in workItems)
        {
            // Only validate items that are "In Progress"
            if (item.State != InProgressState)
            {
                continue;
            }

            // Check if effort is missing (null or 0)
            if (!item.Effort.HasValue || item.Effort.Value == 0)
            {
                result[item.TfsId] = new List<ValidationIssue>
                {
                    new ValidationIssue(
                        ErrorSeverity,
                        "Work item in progress must have effort estimate"
                    )
                };
            }
        }

        return result;
    }
}
