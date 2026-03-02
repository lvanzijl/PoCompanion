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

        // Build a map of parent -> children for efficient lookup
        var childrenLookup = new Dictionary<int, List<WorkItemDto>>();
        foreach (var item in itemsList)
        {
            if (item.ParentTfsId.HasValue)
            {
                if (!childrenLookup.ContainsKey(item.ParentTfsId.Value))
                {
                    childrenLookup[item.ParentTfsId.Value] = new List<WorkItemDto>();
                }
                childrenLookup[item.ParentTfsId.Value].Add(item);
            }
        }

        // Check each parent that is NOT in progress but has children that ARE in progress
        foreach (var item in itemsList)
        {
            var itemClassification = stateClassificationCache[(item.Type, item.State)];
            
            // Only check items that are NOT in progress (potential violators)
            if (itemClassification == StateClassification.InProgress)
            {
                continue;
            }

            // Check if this item has any direct children that are in progress
            if (childrenLookup.TryGetValue(item.TfsId, out var children))
            {
                var inProgressChildren = children
                    .Where(c => stateClassificationCache[(c.Type, c.State)] == StateClassification.InProgress)
                    .ToList();

                if (inProgressChildren.Any())
                {
                    var childList = string.Join(", ", inProgressChildren.Select(c => $"#{c.TfsId} ({c.Type})"));
                    
                    if (!result.ContainsKey(item.TfsId))
                    {
                        result[item.TfsId] = new List<ValidationIssue>();
                    }
                    
                    result[item.TfsId].Add(new ValidationIssue(
                        ErrorSeverity,
                        $"Has children in progress but is not in progress (state: {item.State}). Children: {childList}",
                        "RR-4"
                    ));
                }
            }

            // Check if this item has any descendants (not direct children) that are in progress
            // This generates warnings for ancestor issues
            var allDescendants = GetAllDescendants(item.TfsId, childrenLookup, itemLookup);
            var inProgressDescendants = allDescendants
                .Where(d => stateClassificationCache[(d.Type, d.State)] == StateClassification.InProgress)
                .Where(d => !childrenLookup.ContainsKey(item.TfsId) || 
                           !childrenLookup[item.TfsId].Any(c => c.TfsId == d.TfsId)) // Exclude direct children (already reported as error)
                .ToList();

            if (inProgressDescendants.Any())
            {
                var descendantList = string.Join(", ", inProgressDescendants.Select(d => $"#{d.TfsId} ({d.Type})"));
                
                if (!result.ContainsKey(item.TfsId))
                {
                    result[item.TfsId] = new List<ValidationIssue>();
                }
                
                result[item.TfsId].Add(new ValidationIssue(
                    WarningSeverity,
                    $"Has descendants in progress but is not in progress (state: {item.State}). Descendants: {descendantList}",
                    "RR-4"
                ));
            }
        }

        return result;
    }

    private List<WorkItemDto> GetAllDescendants(int parentId, Dictionary<int, List<WorkItemDto>> childrenLookup, Dictionary<int, WorkItemDto> itemLookup)
    {
        var descendants = new List<WorkItemDto>();
        var queue = new Queue<int>();

        if (childrenLookup.TryGetValue(parentId, out var children))
        {
            foreach (var child in children)
            {
                queue.Enqueue(child.TfsId);
            }
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (itemLookup.TryGetValue(currentId, out var current))
            {
                descendants.Add(current);

                if (childrenLookup.TryGetValue(currentId, out var grandchildren))
                {
                    foreach (var grandchild in grandchildren)
                    {
                        queue.Enqueue(grandchild.TfsId);
                    }
                }
            }
        }

        return descendants;
    }
}
