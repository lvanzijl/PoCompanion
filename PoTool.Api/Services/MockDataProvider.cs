using PoTool.Core.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Provides mock work item data for testing and development.
/// </summary>
public class MockDataProvider
{
    /// <summary>
    /// Generates a complete hierarchy of mock work items from Goals to Tasks.
    /// Includes 12 sprints with varying velocity to demonstrate features.
    /// </summary>
    public List<WorkItemDto> GetMockHierarchy()
    {
        var items = new List<WorkItemDto>();
        var now = DateTimeOffset.UtcNow;

        // =========================
        // GOAL 1: Product Excellence
        // =========================
        items.Add(new WorkItemDto(
            TfsId: 1000,
            Type: WorkItemType.Goal,
            Title: "Deliver High-Quality Product Experience",
            ParentTfsId: null,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // --- Objective 1.1: User Experience ---
        items.Add(new WorkItemDto(
            TfsId: 1001,
            Type: WorkItemType.Objective,
            Title: "Improve User Workflow Efficiency",
            ParentTfsId: 1000,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Epic 1.1.1: Work Item Management
        items.Add(new WorkItemDto(
            TfsId: 1002,
            Type: WorkItemType.Epic,
            Title: "Hierarchical Work Item Tree",
            ParentTfsId: 1001,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Feature 1.1.1.1: Configuration
        items.Add(new WorkItemDto(
            TfsId: 1003,
            Type: WorkItemType.Feature,
            Title: "Goal Configuration UI",
            ParentTfsId: 1002,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1",
            State: "Done",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 1 - Initial setup (low velocity)
        items.Add(new WorkItemDto(TfsId: 1004, Type: WorkItemType.Pbi, Title: "Settings page with Goals editor",
            ParentTfsId: 1003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 3));
        items.Add(new WorkItemDto(TfsId: 1005, Type: WorkItemType.Task, Title: "Create Settings UI with MudBlazor",
            ParentTfsId: 1004, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 4));
        items.Add(new WorkItemDto(TfsId: 1006, Type: WorkItemType.Task, Title: "Implement Goals persistence",
            ParentTfsId: 1004, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 4));

        items.Add(new WorkItemDto(TfsId: 1007, Type: WorkItemType.Pbi, Title: "Basic goal tracking",
            ParentTfsId: 1003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 2));

        // Sprint 2 - Building momentum (medium velocity)
        items.Add(new WorkItemDto(TfsId: 1010, Type: WorkItemType.Pbi, Title: "Multi-selection support",
            ParentTfsId: 1003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));
        items.Add(new WorkItemDto(TfsId: 1011, Type: WorkItemType.Task, Title: "Implement checkbox selection",
            ParentTfsId: 1010, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 6));
        
        items.Add(new WorkItemDto(TfsId: 1012, Type: WorkItemType.Pbi, Title: "Goal filtering options",
            ParentTfsId: 1003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 3));

        // Feature 1.1.1.2: Tree View
        items.Add(new WorkItemDto(
            TfsId: 1020,
            Type: WorkItemType.Feature,
            Title: "Tree View Enhancements",
            ParentTfsId: 1002,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q1",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 3 - Peak velocity
        items.Add(new WorkItemDto(TfsId: 1021, Type: WorkItemType.Pbi, Title: "Collapsible tree nodes",
            ParentTfsId: 1020, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        items.Add(new WorkItemDto(TfsId: 1022, Type: WorkItemType.Task, Title: "Add expand/collapse icons",
            ParentTfsId: 1021, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 3));

        items.Add(new WorkItemDto(TfsId: 1023, Type: WorkItemType.Pbi, Title: "Drag and drop reordering",
            ParentTfsId: 1020, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));
        
        items.Add(new WorkItemDto(TfsId: 1024, Type: WorkItemType.Pbi, Title: "Keyboard navigation",
            ParentTfsId: 1020, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 3));

        // --- Objective 1.2: Performance ---
        items.Add(new WorkItemDto(
            TfsId: 1100,
            Type: WorkItemType.Objective,
            Title: "Optimize Application Performance",
            ParentTfsId: 1000,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q2",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Epic 1.2.1: Data Loading
        items.Add(new WorkItemDto(
            TfsId: 1101,
            Type: WorkItemType.Epic,
            Title: "Efficient Data Loading",
            ParentTfsId: 1100,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q2",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Feature 1.2.1.1: Caching
        items.Add(new WorkItemDto(
            TfsId: 1102,
            Type: WorkItemType.Feature,
            Title: "Client-Side Caching",
            ParentTfsId: 1101,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q2",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 4 - Good velocity maintained
        items.Add(new WorkItemDto(TfsId: 1103, Type: WorkItemType.Pbi, Title: "Implement IndexedDB cache",
            ParentTfsId: 1102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 4",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 1104, Type: WorkItemType.Pbi, Title: "Cache invalidation strategy",
            ParentTfsId: 1102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 4",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));

        items.Add(new WorkItemDto(TfsId: 1105, Type: WorkItemType.Pbi, Title: "Background sync worker",
            ParentTfsId: 1102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 4",
            State: "In Progress", JsonPayload: "{}", RetrievedAt: now, Effort: 5));

        // Sprint 5 - Slight dip (team member out)
        items.Add(new WorkItemDto(TfsId: 1110, Type: WorkItemType.Pbi, Title: "Lazy loading for large datasets",
            ParentTfsId: 1102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 5",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));
        
        items.Add(new WorkItemDto(TfsId: 1111, Type: WorkItemType.Pbi, Title: "Virtual scrolling",
            ParentTfsId: 1102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 5",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 3));

        // Feature 1.2.1.2: Async Operations
        items.Add(new WorkItemDto(
            TfsId: 1120,
            Type: WorkItemType.Feature,
            Title: "Asynchronous Data Processing",
            ParentTfsId: 1101,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q2",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 6 - Recovery and improvement
        items.Add(new WorkItemDto(TfsId: 1121, Type: WorkItemType.Pbi, Title: "Web worker integration",
            ParentTfsId: 1120, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 6",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 1122, Type: WorkItemType.Pbi, Title: "Progress indicators",
            ParentTfsId: 1120, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 6",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 3));
        
        items.Add(new WorkItemDto(TfsId: 1123, Type: WorkItemType.Pbi, Title: "Cancellation token support",
            ParentTfsId: 1120, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q2\\Sprint 6",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));

        // =========================
        // GOAL 2: System Reliability
        // =========================
        items.Add(new WorkItemDto(
            TfsId: 2000,
            Type: WorkItemType.Goal,
            Title: "Ensure System Reliability",
            ParentTfsId: null,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // --- Objective 2.1: Quality ---
        items.Add(new WorkItemDto(
            TfsId: 2001,
            Type: WorkItemType.Objective,
            Title: "Improve Test Coverage",
            ParentTfsId: 2000,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q3",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Epic 2.1.1: Automated Testing
        items.Add(new WorkItemDto(
            TfsId: 2002,
            Type: WorkItemType.Epic,
            Title: "Comprehensive Test Suite",
            ParentTfsId: 2001,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q3",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Feature 2.1.1.1: Unit Tests
        items.Add(new WorkItemDto(
            TfsId: 2003,
            Type: WorkItemType.Feature,
            Title: "Unit Test Coverage",
            ParentTfsId: 2002,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q3",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 7 - Consistent velocity
        items.Add(new WorkItemDto(TfsId: 2004, Type: WorkItemType.Pbi, Title: "Repository layer tests",
            ParentTfsId: 2003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 7",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));
        
        items.Add(new WorkItemDto(TfsId: 2005, Type: WorkItemType.Pbi, Title: "Service layer tests",
            ParentTfsId: 2003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 7",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 2006, Type: WorkItemType.Pbi, Title: "Validation logic tests",
            ParentTfsId: 2003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 7",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 3));

        // Sprint 8 - Stable velocity
        items.Add(new WorkItemDto(TfsId: 2010, Type: WorkItemType.Pbi, Title: "Handler tests with mocks",
            ParentTfsId: 2003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 8",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 2011, Type: WorkItemType.Pbi, Title: "Edge case coverage",
            ParentTfsId: 2003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 8",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));

        // Feature 2.1.1.2: Integration Tests
        items.Add(new WorkItemDto(
            TfsId: 2020,
            Type: WorkItemType.Feature,
            Title: "Integration Test Coverage",
            ParentTfsId: 2002,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q3",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 9 - Higher velocity (team fully ramped)
        items.Add(new WorkItemDto(TfsId: 2021, Type: WorkItemType.Pbi, Title: "API endpoint integration tests",
            ParentTfsId: 2020, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 9",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 2022, Type: WorkItemType.Pbi, Title: "Database integration tests",
            ParentTfsId: 2020, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 9",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));
        
        items.Add(new WorkItemDto(TfsId: 2023, Type: WorkItemType.Pbi, Title: "SignalR hub tests",
            ParentTfsId: 2020, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q3\\Sprint 9",
            State: "Done", JsonPayload: "{}", RetrievedAt: now, Effort: 5));

        // --- Objective 2.2: Monitoring ---
        items.Add(new WorkItemDto(
            TfsId: 2100,
            Type: WorkItemType.Objective,
            Title: "Implement System Monitoring",
            ParentTfsId: 2000,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q4",
            State: "In Progress",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Epic 2.2.1: Logging
        items.Add(new WorkItemDto(
            TfsId: 2101,
            Type: WorkItemType.Epic,
            Title: "Structured Logging",
            ParentTfsId: 2100,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q4",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Feature 2.2.1.1: Application Logging
        items.Add(new WorkItemDto(
            TfsId: 2102,
            Type: WorkItemType.Feature,
            Title: "Comprehensive Application Logs",
            ParentTfsId: 2101,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q4",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 10 - Planning for next quarter
        items.Add(new WorkItemDto(TfsId: 2103, Type: WorkItemType.Pbi, Title: "Serilog integration",
            ParentTfsId: 2102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 10",
            State: "In Progress", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 2104, Type: WorkItemType.Pbi, Title: "Log levels configuration",
            ParentTfsId: 2102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 10",
            State: "New", JsonPayload: "{}", RetrievedAt: now, Effort: 3));

        // Sprint 11 - Active development
        items.Add(new WorkItemDto(TfsId: 2110, Type: WorkItemType.Pbi, Title: "Structured log events",
            ParentTfsId: 2102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 11",
            State: "New", JsonPayload: "{}", RetrievedAt: now, Effort: 5));
        
        items.Add(new WorkItemDto(TfsId: 2111, Type: WorkItemType.Pbi, Title: "Log aggregation setup",
            ParentTfsId: 2102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 11",
            State: "New", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 2112, Type: WorkItemType.Pbi, Title: "Error tracking integration",
            ParentTfsId: 2102, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 11",
            State: "New", JsonPayload: "{}", RetrievedAt: now, Effort: 5));

        // =========================
        // GOAL 3: Developer Experience
        // =========================
        items.Add(new WorkItemDto(
            TfsId: 3000,
            Type: WorkItemType.Goal,
            Title: "Enhance Developer Experience",
            ParentTfsId: null,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // --- Objective 3.1: Documentation ---
        items.Add(new WorkItemDto(
            TfsId: 3001,
            Type: WorkItemType.Objective,
            Title: "Comprehensive Documentation",
            ParentTfsId: 3000,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q4",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Epic 3.1.1: API Documentation
        items.Add(new WorkItemDto(
            TfsId: 3002,
            Type: WorkItemType.Epic,
            Title: "API Documentation",
            ParentTfsId: 3001,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q4",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Feature 3.1.1.1: OpenAPI Spec
        items.Add(new WorkItemDto(
            TfsId: 3003,
            Type: WorkItemType.Feature,
            Title: "OpenAPI Specification",
            ParentTfsId: 3002,
            AreaPath: "PoCompanion\\Product",
            IterationPath: "PoCompanion\\2025\\Q4",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: now,
            Effort: null
        ));

        // Sprint 12 - Future planning
        items.Add(new WorkItemDto(TfsId: 3004, Type: WorkItemType.Pbi, Title: "Swagger UI setup",
            ParentTfsId: 3003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 12",
            State: "New", JsonPayload: "{}", RetrievedAt: now, Effort: 5));
        
        items.Add(new WorkItemDto(TfsId: 3005, Type: WorkItemType.Pbi, Title: "Endpoint documentation",
            ParentTfsId: 3003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 12",
            State: "New", JsonPayload: "{}", RetrievedAt: now, Effort: 8));
        
        items.Add(new WorkItemDto(TfsId: 3006, Type: WorkItemType.Pbi, Title: "Authentication documentation",
            ParentTfsId: 3003, AreaPath: "PoCompanion\\Product", IterationPath: "PoCompanion\\2025\\Q4\\Sprint 12",
            State: "New", JsonPayload: "{}", RetrievedAt: now, Effort: 3));

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
