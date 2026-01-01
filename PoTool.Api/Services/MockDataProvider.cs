using PoTool.Core.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Provides mock work item data for testing and development.
/// </summary>
public class MockDataProvider
{
    /// <summary>
    /// Generates a complete hierarchy of mock work items from Goals to Tasks.
    /// Includes 50 goals, 100 objectives, 200 epics, 300 features, 200 PBIs, 200 bugs, and 1000 tasks.
    /// </summary>
    public List<WorkItemDto> GetMockHierarchy()
    {
        var items = new List<WorkItemDto>();
        var now = DateTimeOffset.UtcNow;
        var idCounter = 1000;

        // Data about categories/domains for generating realistic mock data
        var domains = new[]
        {
            ("Product", new[] { "User Experience", "Performance", "Security", "Scalability", "Innovation" }),
            ("Platform", new[] { "Infrastructure", "DevOps", "Monitoring", "Reliability", "Automation" }),
            ("Data", new[] { "Analytics", "Reporting", "Integration", "Migration", "Quality" }),
            ("Mobile", new[] { "iOS", "Android", "Cross-Platform", "Offline", "Notifications" }),
            ("Web", new[] { "Frontend", "Backend", "API", "Authentication", "Search" }),
            ("AI", new[] { "Machine Learning", "NLP", "Computer Vision", "Recommendations", "Predictions" }),
            ("Compliance", new[] { "GDPR", "Security", "Audit", "Privacy", "Accessibility" }),
            ("Operations", new[] { "Support", "Maintenance", "Documentation", "Training", "Process" }),
            ("Quality", new[] { "Testing", "Automation", "Code Review", "Standards", "Best Practices" }),
            ("Architecture", new[] { "Microservices", "Cloud", "Database", "Cache", "Messaging" })
        };

        var states = new[] { "New", "In Progress", "Done", "Closed" };
        var taskStates = new[] { "New", "In Progress", "Done" };
        var sprints = Enumerable.Range(1, 20).Select(i => $"Sprint {i}").ToArray();
        var quarters = new[] { "Q1", "Q2", "Q3", "Q4" };
        
        // Generate 50 goals (5 per domain)
        var goalCounter = 0;
        foreach (var (domainName, _) in domains)
        {
            for (var g = 0; g < 5; g++)
            {
                var goalId = idCounter++;
                items.Add(new WorkItemDto(
                    TfsId: goalId,
                    Type: WorkItemType.Goal,
                    Title: $"{domainName} Excellence Goal {g + 1}",
                    ParentTfsId: null,
                    AreaPath: $"PoCompanion\\{domainName}",
                    IterationPath: "PoCompanion\\2025",
                    State: g < 2 ? "In Progress" : (g < 4 ? "New" : "Done"),
                    JsonPayload: "{}",
                    RetrievedAt: now,
                    Effort: null
                ));
                
                // Each goal has 2 objectives (100 objectives total)
                for (var o = 0; o < 2; o++)
                {
                    var objectiveId = idCounter++;
                    var quarter = quarters[goalCounter % 4];
                    items.Add(new WorkItemDto(
                        TfsId: objectiveId,
                        Type: WorkItemType.Objective,
                        Title: $"{domainName} Objective {o + 1} for Goal {g + 1}",
                        ParentTfsId: goalId,
                        AreaPath: $"PoCompanion\\{domainName}",
                        IterationPath: $"PoCompanion\\2025\\{quarter}",
                        State: states[goalCounter % states.Length],
                        JsonPayload: "{}",
                        RetrievedAt: now,
                        Effort: null
                    ));
                    
                    // Each objective has 2 epics (200 epics total)
                    for (var e = 0; e < 2; e++)
                    {
                        var epicId = idCounter++;
                        items.Add(new WorkItemDto(
                            TfsId: epicId,
                            Type: WorkItemType.Epic,
                            Title: $"{domainName} Epic {e + 1} - Objective {o + 1}",
                            ParentTfsId: objectiveId,
                            AreaPath: $"PoCompanion\\{domainName}",
                            IterationPath: $"PoCompanion\\2025\\{quarter}",
                            State: states[(idCounter / 10) % states.Length],
                            JsonPayload: "{}",
                            RetrievedAt: now,
                            Effort: null
                        ));
                        
                        // Each epic has ~1.5 features (300 features total: alternate between 1 and 2 features)
                        var featureCount = e % 2 == 0 ? 2 : 1;
                        for (var f = 0; f < featureCount; f++)
                        {
                            var featureId = idCounter++;
                            items.Add(new WorkItemDto(
                                TfsId: featureId,
                                Type: WorkItemType.Feature,
                                Title: $"{domainName} Feature {f + 1} for Epic {e + 1}",
                                ParentTfsId: epicId,
                                AreaPath: $"PoCompanion\\{domainName}",
                                IterationPath: $"PoCompanion\\2025\\{quarter}",
                                State: states[(idCounter / 7) % states.Length],
                                JsonPayload: "{}",
                                RetrievedAt: now,
                                Effort: null
                            ));
                            
                            // Distribute PBIs and Bugs across features
                            // We aim for ~200 PBIs and ~200 Bugs total (400 items across 300 features = ~1.33 per feature)
                            // Alternate: some features get 1 PBI, some get 1 Bug, some get both
                            var itemType = (featureId % 3) switch
                            {
                                0 => new[] { WorkItemType.Pbi },
                                1 => new[] { WorkItemType.Bug },
                                _ => new[] { WorkItemType.Pbi, WorkItemType.Bug }
                            };
                            
                            foreach (var type in itemType)
                            {
                                var workItemId = idCounter++;
                                var sprint = sprints[(idCounter / 5) % sprints.Length];
                                items.Add(new WorkItemDto(
                                    TfsId: workItemId,
                                    Type: type,
                                    Title: $"{type}: {domainName} Work Item for Feature {f + 1}",
                                    ParentTfsId: featureId,
                                    AreaPath: $"PoCompanion\\{domainName}",
                                    IterationPath: $"PoCompanion\\2025\\{quarter}\\{sprint}",
                                    State: states[(idCounter / 3) % states.Length],
                                    JsonPayload: "{}",
                                    RetrievedAt: now,
                                    Effort: idCounter % 2 == 0 ? (idCounter % 13) + 1 : null
                                ));
                                
                                // Each PBI/Bug gets 2-3 tasks (aiming for ~1000 tasks total)
                                // 400 work items * 2.5 tasks = 1000 tasks
                                var taskCount = idCounter % 3 == 0 ? 3 : 2;
                                for (var t = 0; t < taskCount; t++)
                                {
                                    var taskId = idCounter++;
                                    items.Add(new WorkItemDto(
                                        TfsId: taskId,
                                        Type: WorkItemType.Task,
                                        Title: $"Task {t + 1}: {domainName} implementation for {type}",
                                        ParentTfsId: workItemId,
                                        AreaPath: $"PoCompanion\\{domainName}",
                                        IterationPath: $"PoCompanion\\2025\\{quarter}\\{sprint}",
                                        State: taskStates[(idCounter / 2) % taskStates.Length],
                                        JsonPayload: "{}",
                                        RetrievedAt: now,
                                        Effort: (idCounter % 8) + 1
                                    ));
                                }
                            }
                        }
                    }
                }
                goalCounter++;
            }
        }

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
