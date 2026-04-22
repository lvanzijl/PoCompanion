using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services.MockData;

internal static class BattleshipExecutionAnomalySeedCatalog
{
    internal const string TargetProductName = "Incident Response Control";
    internal const string TargetTeamName = "Emergency Protocols";
    internal const string TargetProgramName = "Incident Response";
    internal const string TeamAreaPath = @"\Battleship Systems\Incident Response\Emergency Protocols";
    internal const string ObjectiveTitle = "Execution anomaly validation track";
    internal const string EpicTitle = "Execution anomaly validation track - Sprint signal alignment";
    internal const string FeatureTitle = "Deterministic execution anomaly scenarios";

    private const string ProjectName = "Battleship Systems";
    private const int ScenarioObjectiveId = 900100;
    private const int ScenarioEpicId = 900101;
    private const int ScenarioFeatureId = 900102;
    private const int ScenarioWorkItemIdStart = 901000;

    private static readonly IReadOnlyList<SprintOutcome> Outcomes =
    [
        new(3, 3, 1),
        new(4, 1, 3),
        new(5, 3, 1),
        new(6, 3, 1),
        new(7, 3, 1),
        new(8, 1, 3),
        new(9, 1, 3),
        new(10, 1, 3)
    ];

    private static readonly IReadOnlyDictionary<int, ScenarioWorkItemPlan> PlansByWorkItemId = BuildPlans();

    internal static IReadOnlyList<double> BaselineCommitmentCompletionSeries => [0.75d, 0.75d, 0.75d, 0.75d, 0.75d, 0.75d, 0.75d, 0.75d];
    internal static IReadOnlyList<double> BaselineSpilloverRateSeries => [0.25d, 0.25d, 0.25d, 0.25d, 0.25d, 0.25d, 0.25d, 0.25d];
    internal static IReadOnlyList<double> ScenarioCommitmentCompletionSeries => Outcomes.Select(static outcome => outcome.DoneCount / 4d).ToArray();
    internal static IReadOnlyList<double> ScenarioSpilloverRateSeries => Outcomes.Select(static outcome => outcome.SpillCount / 4d).ToArray();

    internal static IReadOnlyList<WorkItemDto> CreateScenarioHierarchy(DateTimeOffset nowUtc, int targetGoalId)
    {
        var objectiveCreated = nowUtc.AddDays(-160);
        var epicCreated = nowUtc.AddDays(-150);
        var featureCreated = nowUtc.AddDays(-145);

        var scenarioItems = new List<WorkItemDto>
        {
            new(
                TfsId: ScenarioObjectiveId,
                Type: WorkItemType.Objective,
                Title: ObjectiveTitle,
                ParentTfsId: targetGoalId,
                AreaPath: $@"\{ProjectName}\{TargetProgramName}",
                IterationPath: $@"\{ProjectName}\2025\Q2",
                State: "Active",
                RetrievedAt: nowUtc,
                Effort: null,
                Description: "Deterministic Battleship execution anomaly coverage for mock validation.",
                CreatedDate: objectiveCreated,
                ChangedDate: objectiveCreated.AddDays(10),
                BacklogPriority: 1d),
            new(
                TfsId: ScenarioEpicId,
                Type: WorkItemType.Epic,
                Title: EpicTitle,
                ParentTfsId: ScenarioObjectiveId,
                AreaPath: TeamAreaPath,
                IterationPath: @"\Battleship Systems\Backlog",
                State: "Active",
                RetrievedAt: nowUtc,
                Effort: 40,
                Description: "Keeps the execution-anomaly signal path reproducible across the Battleship mock timeline.",
                CreatedDate: epicCreated,
                ChangedDate: epicCreated.AddDays(12),
                Tags: "mock; epic; execution-anomaly; emergency-protocols",
                BacklogPriority: 1d),
            new(
                TfsId: ScenarioFeatureId,
                Type: WorkItemType.Feature,
                Title: FeatureTitle,
                ParentTfsId: ScenarioEpicId,
                AreaPath: TeamAreaPath,
                IterationPath: @"\Battleship Systems\Backlog",
                State: "Active",
                RetrievedAt: nowUtc,
                Effort: 32,
                Description: "Adds deterministic committed-work, completion-variability, and spillover behavior without changing CDC or interpretation logic.",
                CreatedDate: featureCreated,
                ChangedDate: featureCreated.AddDays(8),
                Tags: "mock; feature; execution-anomaly; emergency-protocols",
                BacklogPriority: 1d)
        };

        foreach (var plan in PlansByWorkItemId.Values.OrderBy(static plan => plan.WorkItemId))
        {
            scenarioItems.Add(CreateScenarioPbi(nowUtc, plan));
        }

        return scenarioItems;
    }

    internal static bool TryCreateWorkItemUpdates(
        int workItemId,
        DateTimeOffset nowUtc,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IReadOnlyList<WorkItemUpdate>? updates)
    {
        if (!PlansByWorkItemId.TryGetValue(workItemId, out var plan))
        {
            updates = null;
            return false;
        }

        updates = BuildUpdates(plan, nowUtc);
        return true;
    }

    private static WorkItemDto CreateScenarioPbi(DateTimeOffset nowUtc, ScenarioWorkItemPlan plan)
    {
        var firstSprint = GetSprintIteration(nowUtc, plan.SprintNumbers[0]);
        var currentSprint = GetSprintIteration(nowUtc, plan.SprintNumbers[^1]);
        var completedInSprint = plan.CompletedSprintNumber.HasValue
            ? GetSprintIteration(nowUtc, plan.CompletedSprintNumber.Value)
            : null;

        var createdDate = (firstSprint.StartDate ?? nowUtc).AddDays(-2);
        var changedDate = completedInSprint?.FinishDate?.AddDays(-1)
            ?? currentSprint.StartDate?.AddDays(1)
            ?? nowUtc;
        var state = plan.CompletedSprintNumber.HasValue ? "Done" : "Committed";
        var closedDate = plan.CompletedSprintNumber.HasValue
            ? completedInSprint?.FinishDate?.AddDays(-1)
            : null;

        return new WorkItemDto(
            TfsId: plan.WorkItemId,
            Type: WorkItemType.Pbi,
            Title: $"Execution anomaly work item {plan.WorkItemId}",
            ParentTfsId: ScenarioFeatureId,
            AreaPath: TeamAreaPath,
            IterationPath: currentSprint.Path,
            State: state,
            RetrievedAt: nowUtc,
            Effort: 16,
            Description: $"Deterministic mock PBI spanning sprints {string.Join(" → ", plan.SprintNumbers)}.",
            CreatedDate: createdDate,
            ClosedDate: closedDate,
            Tags: "mock; pbi; execution-anomaly; emergency-protocols",
            ChangedDate: changedDate,
            BusinessValue: 8,
            BacklogPriority: 1d,
            StoryPoints: 8);
    }

    private static IReadOnlyList<WorkItemUpdate> BuildUpdates(ScenarioWorkItemPlan plan, DateTimeOffset nowUtc)
    {
        var firstSprint = GetSprintIteration(nowUtc, plan.SprintNumbers[0]);
        var updates = new List<WorkItemUpdate>
        {
            new()
            {
                WorkItemId = plan.WorkItemId,
                UpdateId = 1,
                RevisedDate = (firstSprint.StartDate ?? nowUtc).AddDays(-2),
                FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["System.Title"] = new("System.Title", null, $"Execution anomaly work item {plan.WorkItemId}"),
                    ["System.State"] = new("System.State", null, "New"),
                    ["System.IterationPath"] = new("System.IterationPath", null, firstSprint.Path)
                }
            },
            new()
            {
                WorkItemId = plan.WorkItemId,
                UpdateId = 2,
                RevisedDate = (firstSprint.StartDate ?? nowUtc).AddDays(-1),
                FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["System.State"] = new("System.State", "New", "Committed")
                }
            }
        };

        var updateId = 3;
        for (var index = 0; index < plan.SprintNumbers.Count - 1; index++)
        {
            var sourceSprint = GetSprintIteration(nowUtc, plan.SprintNumbers[index]);
            var targetSprint = GetSprintIteration(nowUtc, plan.SprintNumbers[index + 1]);

            updates.Add(new WorkItemUpdate
            {
                WorkItemId = plan.WorkItemId,
                UpdateId = updateId++,
                RevisedDate = (sourceSprint.FinishDate ?? nowUtc).AddHours(6),
                FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["System.IterationPath"] = new("System.IterationPath", sourceSprint.Path, targetSprint.Path)
                }
            });
        }

        if (plan.CompletedSprintNumber.HasValue)
        {
            var completedSprint = GetSprintIteration(nowUtc, plan.CompletedSprintNumber.Value);
            updates.Add(new WorkItemUpdate
            {
                WorkItemId = plan.WorkItemId,
                UpdateId = updateId,
                RevisedDate = (completedSprint.FinishDate ?? nowUtc).AddDays(-1),
                FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["System.State"] = new("System.State", "Committed", "Done")
                }
            });
        }

        return updates;
    }

    private static TeamIterationDto GetSprintIteration(DateTimeOffset nowUtc, int sprintNumber)
    {
        return BattleshipSprintSeedCatalog.FindTeamIteration(ProjectName, nowUtc, sprintNumber)
            ?? throw new InvalidOperationException($"Missing Battleship sprint definition for Sprint {sprintNumber}.");
    }

    private static IReadOnlyDictionary<int, ScenarioWorkItemPlan> BuildPlans()
    {
        var plans = new Dictionary<int, ScenarioWorkItemPlan>();
        var carryover = new Queue<ScenarioWorkItemPlan>();
        var nextWorkItemId = ScenarioWorkItemIdStart;

        foreach (var outcome in Outcomes)
        {
            var committed = new List<ScenarioWorkItemPlan>();
            while (carryover.Count > 0)
            {
                committed.Add(carryover.Dequeue());
            }

            while (committed.Count < 4)
            {
                committed.Add(new ScenarioWorkItemPlan(nextWorkItemId++, [outcome.SprintNumber], null));
            }

            var doneItems = committed.Take(outcome.DoneCount).ToList();
            var spillItems = committed.Skip(outcome.DoneCount).Take(outcome.SpillCount).ToList();

            foreach (var done in doneItems)
            {
                plans[done.WorkItemId] = done with { CompletedSprintNumber = outcome.SprintNumber };
            }

            foreach (var spill in spillItems)
            {
                var updated = spill with { SprintNumbers = [.. spill.SprintNumbers, outcome.SprintNumber + 1] };
                plans[spill.WorkItemId] = updated;
                carryover.Enqueue(updated);
            }
        }

        foreach (var open in carryover)
        {
            plans[open.WorkItemId] = open;
        }

        return plans;
    }

    private sealed record SprintOutcome(int SprintNumber, int DoneCount, int SpillCount);

    private sealed record ScenarioWorkItemPlan(
        int WorkItemId,
        IReadOnlyList<int> SprintNumbers,
        int? CompletedSprintNumber);
}
