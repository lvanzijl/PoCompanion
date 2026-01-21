using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validates that when a work item is "In Progress" (based on state classification), its parent must also be "In Progress".
/// Immediate parent violation is an error, ancestor violations are warnings.
/// </summary>
public class WorkItemParentProgressValidator : IWorkItemValidator
{
    private const string ErrorSeverity = "Error";
    private const string WarningSeverity = "Warning";
    private readonly IWorkItemStateClassificationService _stateClassificationService;

    public WorkItemParentProgressValidator(IWorkItemStateClassificationService stateClassificationService)
    {
        _stateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc/>
    public Dictionary<int, List<ValidationIssue>> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var result = new Dictionary<int, List<ValidationIssue>>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();
        var itemLookup = itemsList.ToDictionary(w => w.TfsId);

        // Build a cache of state classifications to avoid repeated async calls
        var stateClassificationCache = new Dictionary<(string Type, string State), StateClassification>();
        foreach (var item in itemsList)
        {
            var key = (item.Type, item.State);
            if (!stateClassificationCache.ContainsKey(key))
            {
                var classification = _stateClassificationService.GetClassificationAsync(item.Type, item.State).GetAwaiter().GetResult();
                stateClassificationCache[key] = classification;
            }
        }

        foreach (var item in itemsList)
        {
            // Only validate items that are "In Progress" (based on state classification)
            var itemClassification = stateClassificationCache[(item.Type, item.State)];
            if (itemClassification != StateClassification.InProgress)
            {
                continue;
            }

            var issues = new List<ValidationIssue>();

            // Check immediate parent
            if (item.ParentTfsId.HasValue)
            {
                if (itemLookup.TryGetValue(item.ParentTfsId.Value, out var parent))
                {
                    var parentClassification = stateClassificationCache[(parent.Type, parent.State)];
                    if (parentClassification != StateClassification.InProgress)
                    {
                        issues.Add(new ValidationIssue(
                            ErrorSeverity,
                            $"Parent '{parent.Type}' is not in progress (state: {parent.State})"
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
                    if (checkedParent)
                    {
                        var ancestorClassification = stateClassificationCache[(ancestor.Type, ancestor.State)];
                        if (ancestorClassification != StateClassification.InProgress)
                        {
                            issues.Add(new ValidationIssue(
                                WarningSeverity,
                                $"Ancestor '{ancestor.Type}' is not in progress (state: {ancestor.State})"
                            ));
                        }
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
