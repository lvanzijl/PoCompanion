using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validates that when a work item is "In Progress", its parent must also be "In Progress".
/// Immediate parent violation is an error, ancestor violations are warnings.
/// </summary>
public class WorkItemParentProgressValidator : IWorkItemValidator
{
    private const string InProgressState = "In Progress";
    private const string ErrorSeverity = "Error";
    private const string WarningSeverity = "Warning";

    /// <inheritdoc/>
    public Dictionary<int, List<ValidationIssue>> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var result = new Dictionary<int, List<ValidationIssue>>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();
        var itemLookup = itemsList.ToDictionary(w => w.TfsId);

        foreach (var item in itemsList)
        {
            // Only validate items that are "In Progress"
            if (item.State != InProgressState)
            {
                continue;
            }

            var issues = new List<ValidationIssue>();

            // Check immediate parent
            if (item.ParentTfsId.HasValue)
            {
                if (itemLookup.TryGetValue(item.ParentTfsId.Value, out var parent))
                {
                    if (parent.State != InProgressState)
                    {
                        issues.Add(new ValidationIssue(
                            ErrorSeverity,
                            $"Parent '{parent.Type}' is not in progress"
                        ));
                    }
                }
            }

            // Check ancestor chain for warnings
            var currentParentId = item.ParentTfsId;
            var checkedParent = false;

            while (currentParentId.HasValue)
            {
                if (itemLookup.TryGetValue(currentParentId.Value, out var ancestor))
                {
                    // Skip immediate parent (already checked as error)
                    if (checkedParent && ancestor.State != InProgressState)
                    {
                        issues.Add(new ValidationIssue(
                            WarningSeverity,
                            $"Ancestor '{ancestor.Type}' is not in progress"
                        ));
                    }

                    checkedParent = true;
                    currentParentId = ancestor.ParentTfsId;
                }
                else
                {
                    // Parent not found in the dataset
                    break;
                }
            }

            if (issues.Any())
            {
                result[item.TfsId] = issues;
            }
        }

        return result;
    }
}
