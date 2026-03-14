using PoTool.Core.Domain.Models;

namespace PoTool.Core.Domain.Sprints;

/// <summary>
/// Provides the fallback canonical state mappings used when no custom settings are configured.
/// </summary>
public static class StateClassificationDefaults
{
    /// <summary>
    /// Creates the default state classifications based on the repository's canonical TFS mappings.
    /// </summary>
    public static IReadOnlyList<WorkItemStateClassification> Create()
    {
        return
        [
            // Goal
            new("goal", "Proposed", StateClassification.New),
            new("goal", "Active", StateClassification.InProgress),
            new("goal", "Completed", StateClassification.Done),
            new("goal", "Removed", StateClassification.Removed),

            // Objective
            new("Objective", "Proposed", StateClassification.New),
            new("Objective", "Active", StateClassification.InProgress),
            new("Objective", "Completed", StateClassification.Done),
            new("Objective", "Removed", StateClassification.Removed),

            // Epic
            new("Epic", "New", StateClassification.New),
            new("Epic", "Active", StateClassification.InProgress),
            new("Epic", "Resolved", StateClassification.Done),
            new("Epic", "Closed", StateClassification.Done),
            new("Epic", "Removed", StateClassification.Removed),

            // Feature
            new("Feature", "New", StateClassification.New),
            new("Feature", "Active", StateClassification.InProgress),
            new("Feature", "Resolved", StateClassification.Done),
            new("Feature", "Closed", StateClassification.Done),
            new("Feature", "Removed", StateClassification.Removed),

            // Product Backlog Item
            new("Product Backlog Item", "New", StateClassification.New),
            new("Product Backlog Item", "Approved", StateClassification.New),
            new("Product Backlog Item", "Committed", StateClassification.InProgress),
            new("Product Backlog Item", "Done", StateClassification.Done),
            new("Product Backlog Item", "Removed", StateClassification.Removed),

            // Bug
            new("Bug", "New", StateClassification.New),
            new("Bug", "Approved", StateClassification.New),
            new("Bug", "Committed", StateClassification.InProgress),
            new("Bug", "Done", StateClassification.Done),
            new("Bug", "Removed", StateClassification.Removed),

            // Task
            new("Task", "To Do", StateClassification.New),
            new("Task", "In Progress", StateClassification.InProgress),
            new("Task", "Done", StateClassification.Done),
            new("Task", "Removed", StateClassification.Removed)
        ];
    }
}
