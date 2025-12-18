using PoTool.Core.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Provides mock work item data for testing and development.
/// </summary>
public class MockDataProvider
{
    /// <summary>
    /// Generates a complete hierarchy of mock work items from Goals to Tasks.
    /// </summary>
    public List<WorkItemDto> GetMockHierarchy()
    {
        var items = new List<WorkItemDto>();
        var now = DateTimeOffset.UtcNow;

        // Goal 1
        items.Add(new WorkItemDto(
            TfsId: 1000,
            Type: WorkItemType.Goal,
            Title: "Deliver High-Quality Product Experience",
            ParentTfsId: null,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Objective 1.1
        items.Add(new WorkItemDto(
            TfsId: 1001,
            Type: WorkItemType.Objective,
            Title: "Improve User Workflow Efficiency",
            ParentTfsId: 1000,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Epic 1.1.1
        items.Add(new WorkItemDto(
            TfsId: 1002,
            Type: WorkItemType.Epic,
            Title: "Hierarchical Work Item Tree",
            ParentTfsId: 1001,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Feature 1.1.1.1
        items.Add(new WorkItemDto(
            TfsId: 1003,
            Type: WorkItemType.Feature,
            Title: "Goal Configuration UI",
            ParentTfsId: 1002,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // PBI 1.1.1.1.1
        items.Add(new WorkItemDto(
            TfsId: 1004,
            Type: WorkItemType.Pbi,
            Title: "Settings page with Goals editor",
            ParentTfsId: 1003,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Task 1.1.1.1.1.1
        items.Add(new WorkItemDto(
            TfsId: 1005,
            Type: WorkItemType.Task,
            Title: "Create Settings UI with MudBlazor",
            ParentTfsId: 1004,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "Done",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Task 1.1.1.1.1.2
        items.Add(new WorkItemDto(
            TfsId: 1006,
            Type: WorkItemType.Task,
            Title: "Implement Goals persistence",
            ParentTfsId: 1004,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Feature 1.1.1.2
        items.Add(new WorkItemDto(
            TfsId: 1007,
            Type: WorkItemType.Feature,
            Title: "Tree View Enhancements",
            ParentTfsId: 1002,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // PBI 1.1.1.2.1
        items.Add(new WorkItemDto(
            TfsId: 1008,
            Type: WorkItemType.Pbi,
            Title: "Multi-selection support",
            ParentTfsId: 1007,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Goal 2
        items.Add(new WorkItemDto(
            TfsId: 2000,
            Type: WorkItemType.Goal,
            Title: "Ensure System Reliability",
            ParentTfsId: null,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        // Objective 2.1
        items.Add(new WorkItemDto(
            TfsId: 2001,
            Type: WorkItemType.Objective,
            Title: "Improve Test Coverage",
            ParentTfsId: 2000,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now
        ));

        return items;
    }

    /// <summary>
    /// Gets mock items for specific goal IDs.
    /// </summary>
    public List<WorkItemDto> GetMockHierarchyForGoals(List<int> goalIds)
    {
        var allItems = GetMockHierarchy();
        return WorkItemHierarchyHelper.FilterDescendants(goalIds, allItems);
    }
}
