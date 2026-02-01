using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validates that work items in "In Progress" state (based on state classification) have an effort estimate.
/// </summary>
public class WorkItemInProgressWithoutEffortValidator : IWorkItemValidator
{
    private const string ErrorSeverity = "Error";
    private readonly IWorkItemStateClassificationService _stateClassificationService;

    public WorkItemInProgressWithoutEffortValidator(IWorkItemStateClassificationService stateClassificationService)
    {
        _stateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
    }

    /// <inheritdoc/>
    public Dictionary<int, List<ValidationIssue>> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var result = new Dictionary<int, List<ValidationIssue>>();
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();

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

            // Check if effort is missing (null or 0)
            if (!item.Effort.HasValue || item.Effort.Value == 0)
            {
                result[item.TfsId] = new List<ValidationIssue>
                {
                    new ValidationIssue(
                        ErrorSeverity,
                        "Work item in progress must have effort estimate",
                        "RC-2"
                    )
                };
            }
        }

        return result;
    }
}
