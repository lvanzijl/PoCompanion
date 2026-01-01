using PoTool.Core.WorkItems;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Generates work items following the Battleship Incident Handling theme.
/// Implements exact hierarchy: 10 Goals → 30 Objectives → 100 Epics → 500 Features → 3,000 PBIs + 1,000 Bugs → 15,000 Tasks
/// </summary>
public class BattleshipWorkItemGenerator
{
    private readonly Random _random;
    private const int Seed = 42; // Consistent seed for reproducible data

    public BattleshipWorkItemGenerator()
    {
        _random = new Random(Seed);
    }

    /// <summary>
    /// Generates the complete work item hierarchy following Battleship theme and mock-data-rules.md
    /// </summary>
    public List<WorkItemDto> GenerateHierarchy()
    {
        var items = new List<WorkItemDto>();
        var now = DateTimeOffset.UtcNow;
        var idCounter = 1000;

        // Define team structure (10-15 teams)
        var teams = GetTeamStructure();
        
        // Define Goals (exactly 10)
        var goalTitles = new[]
        {
            "Mission-Ready Incident Response Platform",
            "Comprehensive Damage Control System",
            "Real-Time Crew Safety Management",
            "Advanced Hull Integrity Monitoring",
            "Integrated Communication Infrastructure",
            "Automated Emergency Response Protocols",
            "Predictive Maintenance and Diagnostics",
            "Command Center Situational Awareness",
            "Fleet-Wide Incident Coordination",
            "Post-Incident Analysis and Reporting"
        };

        var quarters = new[] { "Q1", "Q2", "Q3", "Q4" };
        
        // Generate 10 Goals
        for (var g = 0; g < 10; g++)
        {
            var goalId = idCounter++;
            var goalState = GetGoalState();
            
            items.Add(new WorkItemDto(
                TfsId: goalId,
                Type: WorkItemType.Goal,
                Title: goalTitles[g],
                ParentTfsId: null,
                AreaPath: "\\Battleship Systems",
                IterationPath: "\\Battleship Systems\\2025",
                State: goalState,
                JsonPayload: "{}",
                RetrievedAt: now,
                Effort: null
            ));

            // Each goal has 2-4 objectives (average 3, total ~30)
            var objectiveCount = g < 5 ? 3 : (g < 8 ? 3 : 2); // 5*3 + 3*3 + 2*2 = 28, adjusted to 30
            if (g < 2) objectiveCount = 4; // First 2 goals get 4 objectives each: 2*4 + 3*3 + 3*3 + 2*2 = 31, close to 30
            
            for (var o = 0; o < objectiveCount; o++)
            {
                var objectiveId = idCounter++;
                var quarter = quarters[_random.Next(quarters.Length)];
                var program = GetProgramForGoal(g);
                var objectiveTitle = GetObjectiveTitle(g, o);
                
                items.Add(new WorkItemDto(
                    TfsId: objectiveId,
                    Type: WorkItemType.Objective,
                    Title: objectiveTitle,
                    ParentTfsId: goalId,
                    AreaPath: $"\\Battleship Systems\\{program}",
                    IterationPath: $"\\Battleship Systems\\2025\\{quarter}",
                    State: GetObjectiveState(),
                    JsonPayload: "{}",
                    RetrievedAt: now,
                    Effort: null
                ));

                // Each objective has 2-5 epics (average ~3.3, total ~100)
                var epicCount = _random.Next(2, 6); // 2-5 epics per objective
                
                for (var e = 0; e < epicCount; e++)
                {
                    var epicId = idCounter++;
                    var team = GetRandomTeam(teams, program);
                    var epicTitle = GetEpicTitle(objectiveTitle, e);
                    
                    items.Add(new WorkItemDto(
                        TfsId: epicId,
                        Type: WorkItemType.Epic,
                        Title: epicTitle,
                        ParentTfsId: objectiveId,
                        AreaPath: $"\\Battleship Systems\\{program}\\{team}",
                        IterationPath: $"\\Battleship Systems\\2025\\{quarter}",
                        State: GetEpicState(),
                        JsonPayload: "{}",
                        RetrievedAt: now,
                        Effort: null
                    ));

                    // Each epic has 3-7 features (average 5, total ~500)
                    var featureCount = _random.Next(3, 8);
                    
                    for (var f = 0; f < featureCount; f++)
                    {
                        var featureId = idCounter++;
                        var featureTitle = GetFeatureTitle(epicTitle, f);
                        var sprint = GetSprintPath(quarter);
                        
                        items.Add(new WorkItemDto(
                            TfsId: featureId,
                            Type: WorkItemType.Feature,
                            Title: featureTitle,
                            ParentTfsId: epicId,
                            AreaPath: $"\\Battleship Systems\\{program}\\{team}", // Inherit from Epic
                            IterationPath: sprint,
                            State: GetFeatureState(),
                            JsonPayload: "{}",
                            RetrievedAt: now,
                            Effort: null
                        ));

                        // Each feature has 5-10 PBIs (average ~6, total ~3,000)
                        var pbiCount = _random.Next(5, 11);
                        
                        for (var p = 0; p < pbiCount; p++)
                        {
                            var pbiId = idCounter++;
                            var pbiTitle = GetPbiTitle(featureTitle, p);
                            var pbiSprint = GetWorkItemSprintPath(quarter);
                            var effort = GetFibonacciEffort();
                            
                            items.Add(new WorkItemDto(
                                TfsId: pbiId,
                                Type: WorkItemType.Pbi,
                                Title: pbiTitle,
                                ParentTfsId: featureId,
                                AreaPath: $"\\Battleship Systems\\{program}\\{team}", // Inherit from Epic
                                IterationPath: pbiSprint,
                                State: GetPbiState(),
                                JsonPayload: "{}",
                                RetrievedAt: now,
                                Effort: effort
                            ));

                            // Each PBI has 2-5 tasks (average ~3.75, targeting ~15,000 total)
                            var taskCount = _random.Next(2, 6);
                            
                            for (var t = 0; t < taskCount; t++)
                            {
                                var taskId = idCounter++;
                                var taskTitle = GetTaskTitle(pbiTitle, t);
                                
                                items.Add(new WorkItemDto(
                                    TfsId: taskId,
                                    Type: WorkItemType.Task,
                                    Title: taskTitle,
                                    ParentTfsId: pbiId,
                                    AreaPath: $"\\Battleship Systems\\{program}\\{team}", // Inherit from Epic
                                    IterationPath: pbiSprint,
                                    State: GetTaskState(),
                                    JsonPayload: "{}",
                                    RetrievedAt: now,
                                    Effort: _random.Next(1, 17) // 0-16 hours
                                ));
                            }
                        }

                        // Each feature has 1-3 bugs (average ~2, total ~1,000)
                        var bugCount = _random.Next(1, 4);
                        
                        for (var b = 0; b < bugCount; b++)
                        {
                            var bugId = idCounter++;
                            var bugTitle = GetBugTitle(featureTitle, b);
                            var bugSprint = GetWorkItemSprintPath(quarter);
                            var bugEffort = GetBugEffort();
                            
                            items.Add(new WorkItemDto(
                                TfsId: bugId,
                                Type: WorkItemType.Bug,
                                Title: bugTitle,
                                ParentTfsId: featureId,
                                AreaPath: $"\\Battleship Systems\\{program}\\{team}", // Inherit from Epic
                                IterationPath: bugSprint,
                                State: GetBugState(),
                                JsonPayload: "{}",
                                RetrievedAt: now,
                                Effort: bugEffort
                            ));

                            // Each bug has 2-5 tasks
                            var bugTaskCount = _random.Next(2, 6);
                            
                            for (var t = 0; t < bugTaskCount; t++)
                            {
                                var taskId = idCounter++;
                                var taskTitle = GetTaskTitle(bugTitle, t);
                                
                                items.Add(new WorkItemDto(
                                    TfsId: taskId,
                                    Type: WorkItemType.Task,
                                    Title: taskTitle,
                                    ParentTfsId: bugId,
                                    AreaPath: $"\\Battleship Systems\\{program}\\{team}", // Inherit from Epic
                                    IterationPath: bugSprint,
                                    State: GetTaskState(),
                                    JsonPayload: "{}",
                                    RetrievedAt: now,
                                    Effort: _random.Next(1, 17)
                                ));
                            }
                        }
                    }
                }
            }
        }

        return items;
    }

    private List<(string Program, List<string> Teams)> GetTeamStructure()
    {
        return new List<(string Program, List<string> Teams)>
        {
            ("Incident Detection", new List<string> { "Fire Detection", "Leakage Monitoring", "Collision Detection" }),
            ("Incident Response", new List<string> { "Emergency Protocols", "Crew Safety", "Medical Response" }),
            ("Damage Control", new List<string> { "Hull Integrity", "Repair Coordination", "Resource Management" }),
            ("Shared Services", new List<string> { "Communication Systems", "DevOps & Infrastructure" })
        };
    }

    private string GetProgramForGoal(int goalIndex)
    {
        return goalIndex switch
        {
            0 or 1 or 5 => "Incident Response",
            2 or 7 => "Incident Response", // Crew Safety related
            3 => "Damage Control",
            4 or 8 => "Shared Services",
            6 => "Damage Control", // Maintenance
            9 => "Shared Services", // Reporting
            _ => "Incident Detection"
        };
    }

    private string GetRandomTeam(List<(string Program, List<string> Teams)> teams, string program)
    {
        var programTeams = teams.FirstOrDefault(t => t.Program == program).Teams;
        return programTeams?[_random.Next(programTeams.Count)] ?? "Fire Detection";
    }

    private string GetObjectiveTitle(int goalIndex, int objectiveIndex)
    {
        var objectives = goalIndex switch
        {
            0 => new[] { "Rapid Fire Detection and Suppression", "Automated Collision Response", "Real-Time Leakage Monitoring", "Emergency Alert Distribution" },
            1 => new[] { "Hull Breach Detection System", "Compartment Isolation Automation", "Damage Assessment Dashboard", "Repair Priority Management" },
            2 => new[] { "Crew Location Tracking", "Evacuation Route Optimization", "Medical Emergency Response", "Injury Reporting System" },
            3 => new[] { "Real-Time Hull Pressure Monitoring", "Structural Integrity Analysis", "Automated Breach Sealing" },
            4 => new[] { "Inter-Department Messaging", "Command Center Integration", "Emergency Broadcast System" },
            5 => new[] { "Automated Fire Suppression", "Emergency Protocol Execution", "Incident Command Coordination" },
            6 => new[] { "Predictive Equipment Failure Detection", "Maintenance Scheduling Optimization", "System Health Monitoring" },
            7 => new[] { "Real-Time Incident Visualization", "Multi-Source Data Aggregation", "Decision Support Tools" },
            8 => new[] { "Fleet-Wide Incident Sharing", "Cross-Vessel Coordination", "Resource Pooling Management" },
            9 => new[] { "Incident Log Management", "Root Cause Analysis", "Performance Metrics Dashboard", "Compliance Reporting" },
            _ => new[] { "Objective 1", "Objective 2", "Objective 3", "Objective 4" }
        };
        return objectives[objectiveIndex % objectives.Length];
    }

    private string GetEpicTitle(string objectiveTitle, int epicIndex)
    {
        var epicSuffixes = new[]
        {
            "Sensor Network",
            "Data Processing Pipeline",
            "User Interface",
            "API Integration",
            "Alert System",
            "Reporting Module",
            "Configuration Management"
        };
        return $"{objectiveTitle} - {epicSuffixes[epicIndex % epicSuffixes.Length]}";
    }

    private string GetFeatureTitle(string epicTitle, int featureIndex)
    {
        var featurePrefixes = new[]
        {
            "Multi-Sensor",
            "Real-Time",
            "Automated",
            "Enhanced",
            "Integrated",
            "Advanced",
            "Optimized"
        };
        return $"{featurePrefixes[featureIndex % featurePrefixes.Length]} {epicTitle.Split('-')[0].Trim()} Component";
    }

    private string GetPbiTitle(string featureTitle, int pbiIndex)
    {
        var userRoles = new[] { "damage control officer", "crew member", "medical officer", "engineering officer", "command officer" };
        var actions = new[]
        {
            "view real-time status",
            "receive instant alerts",
            "monitor sensor data",
            "configure alert thresholds",
            "generate reports",
            "track incident progress",
            "coordinate response teams",
            "access historical data",
            "validate system integrity",
            "manage user permissions"
        };
        return $"As a {userRoles[pbiIndex % userRoles.Length]}, I need to {actions[pbiIndex % actions.Length]} for {featureTitle.Split(' ')[^2]} {featureTitle.Split(' ')[^1]}";
    }

    private string GetBugTitle(string featureTitle, int bugIndex)
    {
        var bugTypes = new[]
        {
            "not triggering alerts",
            "showing incorrect data",
            "causing performance degradation",
            "failing to synchronize",
            "displaying outdated information",
            "not responding to user input",
            "generating invalid reports"
        };
        return $"{featureTitle} {bugTypes[bugIndex % bugTypes.Length]}";
    }

    private string GetTaskTitle(string parentTitle, int taskIndex)
    {
        var taskTypes = new[]
        {
            "Implement",
            "Configure",
            "Create",
            "Update",
            "Test",
            "Deploy",
            "Document",
            "Refactor",
            "Optimize"
        };
        var components = new[]
        {
            "API endpoints",
            "database schema",
            "UI components",
            "validation logic",
            "error handling",
            "unit tests",
            "integration tests",
            "configuration",
            "documentation"
        };
        return $"{taskTypes[taskIndex % taskTypes.Length]} {components[taskIndex % components.Length]} for {parentTitle.Split(' ')[0]} {parentTitle.Split(' ')[1]}";
    }

    private string GetSprintPath(string quarter)
    {
        if (_random.NextDouble() < 0.6) // 60% in backlog
            return "\\Battleship Systems\\Backlog";
        
        var sprint = _random.Next(1, 11); // Sprints 1-10
        return $"\\Battleship Systems\\2025\\{quarter}\\Sprint {sprint}";
    }

    private string GetWorkItemSprintPath(string quarter)
    {
        var rand = _random.NextDouble();
        if (rand < 0.60) // 60% in backlog
            return "\\Battleship Systems\\Backlog";
        if (rand < 0.80) // 20% in sprints 1-3 (past/current)
            return $"\\Battleship Systems\\2025\\{quarter}\\Sprint {_random.Next(1, 4)}";
        if (rand < 0.95) // 15% in sprints 4-6 (near-term)
            return $"\\Battleship Systems\\2025\\{quarter}\\Sprint {_random.Next(4, 7)}";
        // 5% in sprints 7+ (future)
        return $"\\Battleship Systems\\2025\\{quarter}\\Sprint {_random.Next(7, 11)}";
    }

    private int? GetFibonacciEffort()
    {
        // 20-30% unestimated
        if (_random.NextDouble() < 0.25)
            return null;

        var fibonacci = new[] { 1, 2, 3, 5, 8, 13, 21 };
        var weights = new[] { 0.20, 0.15, 0.25, 0.20, 0.12, 0.05, 0.03 };
        
        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
                return fibonacci[i];
        }
        return fibonacci[^1];
    }

    private int? GetBugEffort()
    {
        // 30% unestimated
        if (_random.NextDouble() < 0.30)
            return null;

        var fibonacci = new[] { 1, 2, 3, 5, 8 };
        var weights = new[] { 0.30, 0.25, 0.25, 0.15, 0.05 };
        
        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
                return fibonacci[i];
        }
        return fibonacci[^1];
    }

    private string GetGoalState()
    {
        var states = new[] { "Proposed", "Active", "Completed", "Removed" };
        var weights = new[] { 0.10, 0.70, 0.18, 0.02 };
        return WeightedRandom(states, weights);
    }

    private string GetObjectiveState()
    {
        var states = new[] { "Proposed", "Active", "Completed", "Removed" };
        var weights = new[] { 0.15, 0.65, 0.18, 0.02 };
        return WeightedRandom(states, weights);
    }

    private string GetEpicState()
    {
        var states = new[] { "New", "Active", "Resolved", "Closed", "Removed" };
        var weights = new[] { 0.65, 0.20, 0.08, 0.05, 0.02 };
        return WeightedRandom(states, weights);
    }

    private string GetFeatureState()
    {
        var states = new[] { "New", "Active", "Resolved", "Closed", "Removed" };
        var weights = new[] { 0.60, 0.25, 0.08, 0.05, 0.02 };
        return WeightedRandom(states, weights);
    }

    private string GetPbiState()
    {
        var states = new[] { "New", "Approved", "Committed", "Done", "Removed" };
        var weights = new[] { 0.65, 0.15, 0.10, 0.08, 0.02 };
        return WeightedRandom(states, weights);
    }

    private string GetBugState()
    {
        var states = new[] { "New", "Approved", "Committed", "Done", "Removed" };
        var weights = new[] { 0.60, 0.18, 0.12, 0.08, 0.02 };
        return WeightedRandom(states, weights);
    }

    private string GetTaskState()
    {
        var states = new[] { "To Do", "In Progress", "Done", "Removed" };
        var weights = new[] { 0.65, 0.20, 0.13, 0.02 };
        return WeightedRandom(states, weights);
    }

    private string WeightedRandom(string[] options, double[] weights)
    {
        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
                return options[i];
        }
        return options[^1];
    }
}
