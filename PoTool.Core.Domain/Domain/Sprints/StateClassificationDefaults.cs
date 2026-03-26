using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

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
            new(CanonicalWorkItemTypes.Goal, "Proposed", StateClassification.New),
            new(CanonicalWorkItemTypes.Goal, "Active", StateClassification.InProgress),
            new(CanonicalWorkItemTypes.Goal, "Completed", StateClassification.Done),
            new(CanonicalWorkItemTypes.Goal, "Removed", StateClassification.Removed),

            // Objective
            new(CanonicalWorkItemTypes.Objective, "Proposed", StateClassification.New),
            new(CanonicalWorkItemTypes.Objective, "Active", StateClassification.InProgress),
            new(CanonicalWorkItemTypes.Objective, "Completed", StateClassification.Done),
            new(CanonicalWorkItemTypes.Objective, "Removed", StateClassification.Removed),

            // Epic
            new(CanonicalWorkItemTypes.Epic, "New", StateClassification.New),
            new(CanonicalWorkItemTypes.Epic, "Active", StateClassification.InProgress),
            new(CanonicalWorkItemTypes.Epic, "Resolved", StateClassification.Done),
            new(CanonicalWorkItemTypes.Epic, "Closed", StateClassification.Done),
            new(CanonicalWorkItemTypes.Epic, "Removed", StateClassification.Removed),

            // Feature
            new(CanonicalWorkItemTypes.Feature, "New", StateClassification.New),
            new(CanonicalWorkItemTypes.Feature, "Active", StateClassification.InProgress),
            new(CanonicalWorkItemTypes.Feature, "Resolved", StateClassification.Done),
            new(CanonicalWorkItemTypes.Feature, "Closed", StateClassification.Done),
            new(CanonicalWorkItemTypes.Feature, "Removed", StateClassification.Removed),

            // Product Backlog Item
            new(CanonicalWorkItemTypes.Pbi, "New", StateClassification.New),
            new(CanonicalWorkItemTypes.Pbi, "Approved", StateClassification.New),
            new(CanonicalWorkItemTypes.Pbi, "Committed", StateClassification.InProgress),
            new(CanonicalWorkItemTypes.Pbi, "Done", StateClassification.Done),
            new(CanonicalWorkItemTypes.Pbi, "Removed", StateClassification.Removed),

            // Bug
            new(CanonicalWorkItemTypes.Bug, "New", StateClassification.New),
            new(CanonicalWorkItemTypes.Bug, "Approved", StateClassification.New),
            new(CanonicalWorkItemTypes.Bug, "Committed", StateClassification.InProgress),
            new(CanonicalWorkItemTypes.Bug, "Done", StateClassification.Done),
            new(CanonicalWorkItemTypes.Bug, "Removed", StateClassification.Removed),

            // Task
            new(CanonicalWorkItemTypes.Task, "To Do", StateClassification.New),
            new(CanonicalWorkItemTypes.Task, "In Progress", StateClassification.InProgress),
            new(CanonicalWorkItemTypes.Task, "Done", StateClassification.Done),
            new(CanonicalWorkItemTypes.Task, "Removed", StateClassification.Removed)
        ];
    }
}
