using PoTool.Shared.Settings;

namespace PoTool.Core.Domain.Sprints;

/// <summary>
/// Provides the fallback canonical state mappings used when no custom settings are configured.
/// </summary>
public static class StateClassificationDefaults
{
    /// <summary>
    /// Creates the default state classifications based on the repository's canonical TFS mappings.
    /// </summary>
    public static IReadOnlyList<WorkItemStateClassificationDto> Create()
    {
        return
        [
            // Goal
            new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Proposed", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Active", Classification = StateClassification.InProgress },
            new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Completed", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Removed", Classification = StateClassification.Removed },

            // Objective
            new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Proposed", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Active", Classification = StateClassification.InProgress },
            new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Completed", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Removed", Classification = StateClassification.Removed },

            // Epic
            new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "New", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Active", Classification = StateClassification.InProgress },
            new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Resolved", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Closed", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Removed", Classification = StateClassification.Removed },

            // Feature
            new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "New", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Active", Classification = StateClassification.InProgress },
            new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Resolved", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Closed", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Removed", Classification = StateClassification.Removed },

            // Product Backlog Item
            new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "New", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Approved", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Committed", Classification = StateClassification.InProgress },
            new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Done", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Removed", Classification = StateClassification.Removed },

            // Bug
            new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "New", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Approved", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Committed", Classification = StateClassification.InProgress },
            new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Done", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Removed", Classification = StateClassification.Removed },

            // Task
            new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "To Do", Classification = StateClassification.New },
            new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "In Progress", Classification = StateClassification.InProgress },
            new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "Done", Classification = StateClassification.Done },
            new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "Removed", Classification = StateClassification.Removed }
        ];
    }
}
