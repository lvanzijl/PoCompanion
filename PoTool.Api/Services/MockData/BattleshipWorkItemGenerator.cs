using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Generates work items following the Battleship Incident Handling theme.
/// Implements exact hierarchy: 10 Goals → 30 Objectives → 100 Epics → 500 Features → 3,000 PBIs + 1,000 Bugs → 15,000 Tasks.
/// The generated dataset is intentionally realistic rather than uniformly valid so analytics surfaces show useful variation.
/// </summary>
public class BattleshipWorkItemGenerator
{
    private static readonly int[] StoryPointValues = [1, 2, 3, 5, 8, 13, 21];
    private static readonly double[] StoryPointWeights = [0.14, 0.18, 0.21, 0.21, 0.15, 0.08, 0.03];
    private static readonly int[] PbiEffortHours = [4, 8, 12, 16, 24, 32, 40, 60];
    private static readonly double[] PbiEffortWeights = [0.10, 0.16, 0.20, 0.18, 0.16, 0.10, 0.07, 0.03];
    private static readonly int[] BugEffortHours = [2, 4, 8, 12, 16, 24];
    private static readonly double[] BugEffortWeights = [0.17, 0.24, 0.24, 0.18, 0.12, 0.05];
    private static readonly int[] ParentEffortHours = [16, 24, 32, 40, 60, 80, 120];
    private static readonly double[] ParentEffortWeights = [0.08, 0.14, 0.19, 0.19, 0.18, 0.14, 0.08];
    private static readonly int[] TaskEffortHours = [2, 4, 6, 8, 12, 16];
    private static readonly double[] TaskEffortWeights = [0.18, 0.24, 0.22, 0.18, 0.12, 0.06];
    private readonly Random _random;
    private const int Seed = 42;

    public BattleshipWorkItemGenerator()
    {
        _random = new Random(Seed);
    }

    /// <summary>
    /// Generates the complete work item hierarchy for mock mode.
    /// </summary>
    public List<WorkItemDto> GenerateHierarchy()
    {
        var items = new List<WorkItemDto>();
        var now = DateTimeOffset.UtcNow;
        var idCounter = 1000;
        var teams = GetTeamStructure();

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

        for (var g = 0; g < goalTitles.Length; g++)
        {
            var goalId = idCounter++;
            var goalState = GetGoalState();
            var goalCreatedDate = GetCreatedDate("\\Battleship Systems\\2025", now);
            var goalChangedDate = GetChangedDate(goalCreatedDate, now);

            items.Add(new WorkItemDto(
                TfsId: goalId,
                Type: WorkItemType.Goal,
                Title: goalTitles[g],
                ParentTfsId: null,
                AreaPath: "\\Battleship Systems",
                IterationPath: "\\Battleship Systems\\2025",
                State: goalState,
                RetrievedAt: now,
                Effort: null,
                Description: $"Strategic goal for {goalTitles[g].ToLowerInvariant()} across the mock portfolio.",
                CreatedDate: goalCreatedDate,
                ClosedDate: GetClosedDate(goalChangedDate, goalState),
                Tags: BuildTags("mock", "portfolio", "goal"),
                ChangedDate: goalChangedDate,
                BacklogPriority: GetBacklogPriority()));

            var objectiveCount = g < 5 ? 3 : (g < 8 ? 3 : 2);
            if (g < 2)
            {
                objectiveCount = 4;
            }

            for (var o = 0; o < objectiveCount; o++)
            {
                var objectiveId = idCounter++;
                var quarter = GetQuarterForGoal(g, o);
                var program = GetProgramForGoal(g);
                var objectiveTitle = GetObjectiveTitle(g, o);
                var objectiveState = GetObjectiveState();
                var objectiveIterationPath = $"\\Battleship Systems\\2025\\{quarter}";
                var objectiveCreatedDate = GetCreatedDate(objectiveIterationPath, now);
                var objectiveChangedDate = GetChangedDate(objectiveCreatedDate, now);

                items.Add(new WorkItemDto(
                    TfsId: objectiveId,
                    Type: WorkItemType.Objective,
                    Title: objectiveTitle,
                    ParentTfsId: goalId,
                    AreaPath: $"\\Battleship Systems\\{program}",
                    IterationPath: objectiveIterationPath,
                    State: objectiveState,
                    RetrievedAt: now,
                    Effort: null,
                    Description: $"Objective to operationalize {objectiveTitle.ToLowerInvariant()} for {program.ToLowerInvariant()} teams.",
                    CreatedDate: objectiveCreatedDate,
                    ClosedDate: GetClosedDate(objectiveChangedDate, objectiveState),
                    Tags: BuildTags("mock", "portfolio", "objective"),
                    ChangedDate: objectiveChangedDate,
                    BacklogPriority: GetBacklogPriority()));

                var epicCount = _random.Next(2, 6);
                for (var e = 0; e < epicCount; e++)
                {
                    var epicId = idCounter++;
                    var team = GetRandomTeam(teams, program);
                    var epicTitle = GetEpicTitle(objectiveTitle, e);
                    var epicIterationPath = GetPlanningPath();
                    var epicState = GetEpicState();
                    var epicCreatedDate = GetCreatedDate(epicIterationPath, now);
                    var epicChangedDate = GetChangedDate(epicCreatedDate, now);
                    var epicDescription = MaybeBlankDescription(
                        $"Epic covering {epicTitle.ToLowerInvariant()} and the downstream delivery dependencies it creates.",
                        epicState,
                        0.03);

                    items.Add(new WorkItemDto(
                        TfsId: epicId,
                        Type: WorkItemType.Epic,
                        Title: epicTitle,
                        ParentTfsId: objectiveId,
                        AreaPath: $"\\Battleship Systems\\{program}\\{team}",
                        IterationPath: epicIterationPath,
                        State: epicState,
                        RetrievedAt: now,
                        Effort: GetParentEffort(epicState, 0.90),
                        Description: epicDescription,
                        CreatedDate: epicCreatedDate,
                        ClosedDate: GetClosedDate(epicChangedDate, epicState),
                        Tags: GetEpicTags(program, team, e),
                        IsBlocked: GetBlockedFlag(epicState, 0.04),
                        ChangedDate: epicChangedDate,
                        BacklogPriority: GetBacklogPriority()));

                    var featureCount = _random.Next(3, 8);
                    for (var f = 0; f < featureCount; f++)
                    {
                        var featureId = idCounter++;
                        var featureTitle = GetFeatureTitle(epicTitle, f);
                        var featureIterationPath = GetPlanningPath();
                        var forceStateMismatch = _random.NextDouble() < 0.025;
                        var featureState = forceStateMismatch ? "Closed" : GetFeatureState();
                        var featureCreatedDate = GetCreatedDate(featureIterationPath, now);
                        var featureChangedDate = GetChangedDate(featureCreatedDate, now);
                        var featureDescription = MaybeBlankDescription(
                            $"Feature that delivers {featureTitle.ToLowerInvariant()} with operational support workflows.",
                            featureState,
                            0.04);
                        var createBrokenHierarchy = !IsTerminalState(featureState) && _random.NextDouble() < 0.02;

                        items.Add(new WorkItemDto(
                            TfsId: featureId,
                            Type: WorkItemType.Feature,
                            Title: featureTitle,
                            ParentTfsId: epicId,
                            AreaPath: $"\\Battleship Systems\\{program}\\{team}",
                            IterationPath: featureIterationPath,
                            State: featureState,
                            RetrievedAt: now,
                            Effort: GetParentEffort(featureState, 0.92),
                            Description: featureDescription,
                            CreatedDate: featureCreatedDate,
                            ClosedDate: GetClosedDate(featureChangedDate, featureState),
                            Tags: BuildTags("mock", "feature", NormalizeTag(team)),
                            IsBlocked: GetBlockedFlag(featureState, 0.06),
                            ChangedDate: featureChangedDate,
                            BacklogPriority: GetBacklogPriority()));

                        var pbiCount = createBrokenHierarchy ? 0 : _random.Next(5, 11);
                        for (var p = 0; p < pbiCount; p++)
                        {
                            var pbiId = idCounter++;
                            var pbiTitle = GetPbiTitle(featureTitle, p);
                            var pbiIterationPath = GetDeliveryPath();
                            var pbiState = forceStateMismatch && p == 0 ? "Committed" : GetPbiState();
                            var pbiCreatedDate = GetCreatedDate(pbiIterationPath, now);
                            var pbiChangedDate = GetChangedDate(pbiCreatedDate, now);
                            var pbiSizing = GetPbiSizing(pbiState);
                            var pbiDescription = MaybeBlankDescription(
                                $"User-facing delivery slice for {pbiTitle.ToLowerInvariant()} with acceptance criteria and operational context.",
                                pbiState,
                                0.05);

                            items.Add(new WorkItemDto(
                                TfsId: pbiId,
                                Type: WorkItemType.Pbi,
                                Title: pbiTitle,
                                ParentTfsId: featureId,
                                AreaPath: $"\\Battleship Systems\\{program}\\{team}",
                                IterationPath: pbiIterationPath,
                                State: pbiState,
                                RetrievedAt: now,
                                Effort: pbiSizing.EffortHours,
                                Description: pbiDescription,
                                CreatedDate: pbiCreatedDate,
                                ClosedDate: GetClosedDate(pbiChangedDate, pbiState),
                                Tags: BuildTags("mock", "pbi", NormalizeTag(team)),
                                IsBlocked: GetBlockedFlag(pbiState, 0.08),
                                ChangedDate: pbiChangedDate,
                                BusinessValue: pbiSizing.BusinessValue,
                                BacklogPriority: GetBacklogPriority(),
                                StoryPoints: pbiSizing.StoryPoints));

                            var taskCount = _random.Next(2, 6);
                            for (var t = 0; t < taskCount; t++)
                            {
                                var taskId = idCounter++;
                                var taskTitle = GetTaskTitle(pbiTitle, t);
                                var taskState = forceStateMismatch && p == 0 && t == 0 ? "In Progress" : GetTaskState();
                                var taskCreatedDate = GetCreatedDate(pbiIterationPath, now);
                                var taskChangedDate = GetChangedDate(taskCreatedDate, now);

                                items.Add(new WorkItemDto(
                                    TfsId: taskId,
                                    Type: WorkItemType.Task,
                                    Title: taskTitle,
                                    ParentTfsId: pbiId,
                                    AreaPath: $"\\Battleship Systems\\{program}\\{team}",
                                    IterationPath: pbiIterationPath,
                                    State: taskState,
                                    RetrievedAt: now,
                                    Effort: WeightedRandom(TaskEffortHours, TaskEffortWeights),
                                    Description: $"Implementation task to {taskTitle.ToLowerInvariant()}.",
                                    CreatedDate: taskCreatedDate,
                                    ClosedDate: GetClosedDate(taskChangedDate, taskState),
                                    Tags: BuildTags("mock", "task", NormalizeTag(team)),
                                    ChangedDate: taskChangedDate,
                                    BacklogPriority: GetBacklogPriority()));
                            }
                        }

                        var bugCount = _random.Next(1, 4);
                        for (var b = 0; b < bugCount; b++)
                        {
                            var bugId = idCounter++;
                            var bugTitle = GetBugTitle(featureTitle, b);
                            var bugIterationPath = GetDeliveryPath();
                            var bugState = GetBugState();
                            var bugCreatedDate = GetCreatedDate(bugIterationPath, now);
                            var bugChangedDate = GetChangedDate(bugCreatedDate, now);

                            items.Add(new WorkItemDto(
                                TfsId: bugId,
                                Type: WorkItemType.Bug,
                                Title: bugTitle,
                                ParentTfsId: featureId,
                                AreaPath: $"\\Battleship Systems\\{program}\\{team}",
                                IterationPath: bugIterationPath,
                                State: bugState,
                                RetrievedAt: now,
                                Effort: GetBugEffort(bugState),
                                Description: MaybeBlankDescription($"Operational defect report for {bugTitle.ToLowerInvariant()}.", bugState, 0.04),
                                CreatedDate: bugCreatedDate,
                                ClosedDate: GetClosedDate(bugChangedDate, bugState),
                                Severity: GetBugSeverity(),
                                Tags: GetBugTags(team, bugState, b),
                                IsBlocked: GetBlockedFlag(bugState, 0.12),
                                ChangedDate: bugChangedDate,
                                BacklogPriority: GetBacklogPriority()));

                            var bugTaskCount = _random.Next(2, 6);
                            for (var t = 0; t < bugTaskCount; t++)
                            {
                                var taskId = idCounter++;
                                var bugTaskTitle = GetTaskTitle(bugTitle, t);
                                var bugTaskState = GetTaskState();
                                var bugTaskCreatedDate = GetCreatedDate(bugIterationPath, now);
                                var bugTaskChangedDate = GetChangedDate(bugTaskCreatedDate, now);

                                items.Add(new WorkItemDto(
                                    TfsId: taskId,
                                    Type: WorkItemType.Task,
                                    Title: bugTaskTitle,
                                    ParentTfsId: bugId,
                                    AreaPath: $"\\Battleship Systems\\{program}\\{team}",
                                    IterationPath: bugIterationPath,
                                    State: bugTaskState,
                                    RetrievedAt: now,
                                    Effort: WeightedRandom(TaskEffortHours, TaskEffortWeights),
                                    Description: $"Investigation task to {bugTaskTitle.ToLowerInvariant()}.",
                                    CreatedDate: bugTaskCreatedDate,
                                    ClosedDate: GetClosedDate(bugTaskChangedDate, bugTaskState),
                                    Tags: BuildTags("mock", "task", "bugfix", NormalizeTag(team)),
                                    ChangedDate: bugTaskChangedDate,
                                    BacklogPriority: GetBacklogPriority()));
                            }
                        }
                    }
                }
            }
        }

        AppendExecutionAnomalyScenario(items, now);
        return items;
    }

    private static void AppendExecutionAnomalyScenario(List<WorkItemDto> items, DateTimeOffset now)
    {
        var targetGoalId = items
            .Where(item => item.Type == WorkItemType.Goal)
            .Where(item => string.Equals(item.Title, "Mission-Ready Incident Response Platform", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.TfsId)
            .Single();

        items.AddRange(BattleshipExecutionAnomalySeedCatalog.CreateScenarioHierarchy(now, targetGoalId));
    }

    internal static List<(string Program, List<string> Teams)> GetTeamStructure()
    {
        return
        [
            ("Incident Detection", new List<string> { "Fire Detection", "Leakage Monitoring", "Collision Detection" }),
            ("Incident Response", new List<string> { "Emergency Protocols", "Crew Safety", "Medical Response" }),
            ("Damage Control", new List<string> { "Hull Integrity", "Repair Coordination", "Resource Management" }),
            ("Shared Services", new List<string> { "Communication Systems", "DevOps & Infrastructure" })
        ];
    }

    private string GetProgramForGoal(int goalIndex)
    {
        return goalIndex switch
        {
            0 or 1 or 5 => "Incident Response",
            2 or 7 => "Incident Response",
            3 => "Damage Control",
            4 or 8 => "Shared Services",
            6 => "Damage Control",
            9 => "Shared Services",
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
        var taskTypes = new[] { "Implement", "Configure", "Create", "Update", "Test", "Deploy", "Document", "Refactor", "Optimize" };
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

    private string GetPlanningPath()
    {
        return _random.NextDouble() < 0.45
            ? "\\Battleship Systems\\Backlog"
            : GetSprintPath(GetPlanningSprintNumber());
    }

    private string GetDeliveryPath()
    {
        var roll = _random.NextDouble();
        if (roll < 0.25)
        {
            return "\\Battleship Systems\\Backlog";
        }

        if (roll < 0.75)
        {
            return GetSprintPath(GetDeliverySprintNumber());
        }

        if (roll < 0.92)
        {
            return GetSprintPath(_random.Next(13, 15));
        }

        return GetSprintPath(_random.Next(10, 15));
    }

    private string GetSprintPath(int sprintNumber)
    {
        return $"\\Battleship Systems\\Sprint {sprintNumber}";
    }

    private string GetQuarterForGoal(int goalIndex, int objectiveIndex)
    {
        return (goalIndex + objectiveIndex) % 2 == 0 ? "Q1" : "Q2";
    }

    private int GetSprintNumber()
    {
        return WeightedRandom([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], [0.06, 0.08, 0.11, 0.13, 0.14, 0.15, 0.12, 0.10, 0.07, 0.04]);
    }

    private int GetPlanningSprintNumber()
    {
        return WeightedRandom([10, 11, 12, 13, 14], [0.12, 0.30, 0.28, 0.18, 0.12]);
    }

    private int GetDeliverySprintNumber()
    {
        return WeightedRandom([10, 11, 12, 13, 14], [0.14, 0.34, 0.24, 0.16, 0.12]);
    }

    private int? GetParentEffort(string state, double estimatedProbability)
    {
        if (!IsTerminalState(state) && _random.NextDouble() >= estimatedProbability)
        {
            return null;
        }

        return WeightedRandom(ParentEffortHours, ParentEffortWeights);
    }

    private int? GetBugEffort(string state)
    {
        if (!IsTerminalState(state) && _random.NextDouble() < 0.10)
        {
            return null;
        }

        return WeightedRandom(BugEffortHours, BugEffortWeights);
    }

    private PbiSizing GetPbiSizing(string state)
    {
        var hasEffort = IsTerminalState(state) || _random.NextDouble() < 0.91;
        var effortHours = hasEffort ? WeightedRandom(PbiEffortHours, PbiEffortWeights) : (int?)null;

        if (IsClosedState(state) && _random.NextDouble() < 0.04)
        {
            return new PbiSizing(effortHours, 0, null);
        }

        var sizingMode = _random.NextDouble();
        if (sizingMode < 0.76)
        {
            return new PbiSizing(effortHours, WeightedRandom(StoryPointValues, StoryPointWeights), GetBusinessValue());
        }

        if (sizingMode < 0.86)
        {
            return new PbiSizing(effortHours, null, WeightedRandom(StoryPointValues, StoryPointWeights));
        }

        if (sizingMode < 0.93)
        {
            return new PbiSizing(effortHours, null, null);
        }

        return new PbiSizing(null, null, null);
    }

    private int GetBusinessValue()
    {
        return WeightedRandom([1, 2, 3, 5, 8], [0.10, 0.18, 0.34, 0.26, 0.12]);
    }

    private DateTimeOffset GetCreatedDate(string iterationPath, DateTimeOffset now)
    {
        var sprintNumber = TryGetSprintNumber(iterationPath);
        if (sprintNumber.HasValue)
        {
            var baseline = sprintNumber.Value <= 6
                ? now.AddDays((sprintNumber.Value - 6) * 14)
                : now.AddDays(-_random.Next(1, 21));
            return baseline.AddDays(-_random.Next(10, 70)).AddHours(-_random.Next(0, 24));
        }

        if (iterationPath.Contains(@"\2025", StringComparison.OrdinalIgnoreCase))
        {
            return now.AddDays(-_random.Next(90, 240)).AddHours(-_random.Next(0, 24));
        }

        return now.AddDays(-_random.Next(21, 210)).AddHours(-_random.Next(0, 24));
    }

    private DateTimeOffset GetChangedDate(DateTimeOffset createdDate, DateTimeOffset now)
    {
        var maxDays = Math.Max(1, (int)Math.Floor((now - createdDate).TotalDays));
        var changedDate = createdDate.AddDays(_random.Next(1, Math.Min(maxDays + 1, 90))).AddHours(_random.Next(0, 12));
        return changedDate > now ? now.AddHours(-_random.Next(1, 48)) : changedDate;
    }

    private DateTimeOffset? GetClosedDate(DateTimeOffset changedDate, string state)
    {
        return IsClosedState(state) ? changedDate : null;
    }

    private bool? GetBlockedFlag(string state, double probability)
    {
        return !IsTerminalState(state) && _random.NextDouble() < probability;
    }

    private string? MaybeBlankDescription(string description, string state, double blankProbability)
    {
        return !IsTerminalState(state) && _random.NextDouble() < blankProbability
            ? null
            : description;
    }

    private double GetBacklogPriority()
    {
        return Math.Round(_random.NextDouble() * 100, 2);
    }

    private string GetBugSeverity()
    {
        return WeightedRandom(["Low", "Medium", "High", "Critical"], [0.28, 0.37, 0.25, 0.10]);
    }

    private int? TryGetSprintNumber(string iterationPath)
    {
        const string marker = "Sprint ";
        var index = iterationPath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var numberStart = index + marker.Length;
        var digits = new string(iterationPath[numberStart..].TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var sprintNumber) ? sprintNumber : null;
    }

    private bool IsTerminalState(string state)
    {
        return IsClosedState(state) || string.Equals(state, "Removed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClosedState(string state)
    {
        return state is "Completed" or "Resolved" or "Closed" or "Done";
    }

    private string NormalizeTag(string value)
    {
        return value.ToLowerInvariant().Replace(' ', '-').Replace('&', '-');
    }

    private string GetEpicTags(string program, string team, int epicIndex)
    {
        var tags = new List<string>
        {
            "mock",
            "epic",
            NormalizeTag(program),
            NormalizeTag(team)
        };

        if (epicIndex < 2)
        {
            tags.Add("roadmap");
            tags.Add(epicIndex == 0 ? "priority-high" : "priority-medium");
        }
        else if (epicIndex == 2)
        {
            tags.Add("priority-low");
        }

        return BuildTags(tags.ToArray());
    }

    private string GetBugTags(string team, string bugState, int bugIndex)
    {
        var tags = new List<string>
        {
            "mock",
            "bug",
            NormalizeTag(team),
            MockBugTriageTags[(bugIndex + team.Length) % MockBugTriageTags.Length]
        };

        if (!IsTerminalState(bugState))
        {
            tags.Add("Needs Investigation");
        }

        if (bugIndex % 3 == 0)
        {
            tags.Add("Regression");
        }

        return BuildTags(tags.ToArray());
    }

    private static string BuildTags(params string[] tags)
    {
        return string.Join(
            "; ",
            tags.Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static readonly string[] MockBugTriageTags =
    [
        "Customer Reported",
        "Regression",
        "Operational Risk",
        "Hotfix Candidate",
        "Needs Repro"
    ];

    private string GetGoalState()
    {
        return WeightedRandom(["Proposed", "Active", "Completed", "Removed"], [0.08, 0.68, 0.21, 0.03]);
    }

    private string GetObjectiveState()
    {
        return WeightedRandom(["Proposed", "Active", "Completed", "Removed"], [0.10, 0.63, 0.24, 0.03]);
    }

    private string GetEpicState()
    {
        return WeightedRandom(["New", "Active", "Resolved", "Closed", "Removed"], [0.46, 0.30, 0.12, 0.08, 0.04]);
    }

    private string GetFeatureState()
    {
        return WeightedRandom(["New", "Active", "Resolved", "Closed", "Removed"], [0.43, 0.32, 0.12, 0.09, 0.04]);
    }

    private string GetPbiState()
    {
        return WeightedRandom(["New", "Approved", "Committed", "Done", "Removed"], [0.35, 0.23, 0.21, 0.17, 0.04]);
    }

    private string GetBugState()
    {
        return WeightedRandom(["New", "Approved", "Committed", "Done", "Removed"], [0.31, 0.22, 0.18, 0.23, 0.06]);
    }

    private string GetTaskState()
    {
        return WeightedRandom(["To Do", "In Progress", "Done", "Removed"], [0.39, 0.34, 0.22, 0.05]);
    }

    private string WeightedRandom(string[] options, double[] weights)
    {
        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
            {
                return options[i];
            }
        }

        return options[^1];
    }

    private int WeightedRandom(int[] options, double[] weights)
    {
        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
            {
                return options[i];
            }
        }

        return options[^1];
    }

    private readonly record struct PbiSizing(int? EffortHours, int? StoryPoints, int? BusinessValue);
}
