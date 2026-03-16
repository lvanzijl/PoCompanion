using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class SprintCommitmentCdcServicesTests
{
    [TestMethod]
    public void SprintCommitmentService_BuildCommitments_UsesCanonicalCommitmentTimestamp()
    {
        var sprint = CreateSprint();
        var service = new SprintCommitmentService();
        var commitmentTimestamp = service.GetCommitmentTimestamp(sprint.StartUtc!.Value);
        var workItemsById = new Dictionary<int, WorkItemSnapshot>
        {
            [101] = new(101, WorkItemType.Pbi, "Active", "\\Project\\Backlog")
        };
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
        {
            [101] =
            [
                CreateFieldChangeEvent(101, "System.IterationPath", commitmentTimestamp.AddHours(1), "\\Project\\Sprint 1", "\\Project\\Backlog")
            ]
        };

        var commitments = service.BuildCommitments(sprint, workItemsById, iterationEventsByWorkItem);

        Assert.HasCount(1, commitments);
        Assert.AreEqual(101, commitments[0].WorkItemId);
        Assert.AreEqual(commitmentTimestamp, commitments[0].CommitmentTimestamp);
    }

    [TestMethod]
    public void SprintScopeChangeService_DetectsAddedAndRemovedEventsWithinCommitmentWindow()
    {
        var sprint = CreateSprint();
        var commitmentTimestamp = SprintCommitmentLookup.GetCommitmentTimestamp(sprint.StartUtc!.Value);
        var service = new SprintScopeChangeService();
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
        {
            [101] =
            [
                CreateFieldChangeEvent(101, "System.IterationPath", commitmentTimestamp.AddHours(2), "\\Project\\Backlog", sprint.Path)
            ],
            [102] =
            [
                CreateFieldChangeEvent(102, "System.IterationPath", commitmentTimestamp.AddHours(3), sprint.Path, "\\Project\\Backlog")
            ]
        };

        var added = service.DetectScopeAdded(sprint, iterationEventsByWorkItem);
        var removed = service.DetectScopeRemoved(sprint, iterationEventsByWorkItem);

        Assert.HasCount(1, added);
        Assert.AreEqual(101, added[0].WorkItemId);
        Assert.HasCount(1, removed);
        Assert.AreEqual(102, removed[0].WorkItemId);
    }

    [TestMethod]
    public void SprintCompletionService_DetectCompletions_FiltersToSprintWindow()
    {
        var sprint = CreateSprint();
        var service = new SprintCompletionService();

        var completions = service.DetectCompletions(
            sprint,
            new Dictionary<int, DateTimeOffset>
            {
                [101] = sprint.StartUtc!.Value.AddDays(2),
                [102] = sprint.EndUtc!.Value.AddDays(1)
            });

        Assert.HasCount(1, completions);
        Assert.AreEqual(101, completions[0].WorkItemId);
    }

    [TestMethod]
    public void SprintSpilloverService_DetectSpillover_ReturnsDirectMoveTimestamp()
    {
        var sprint = CreateSprint();
        var sprintEnd = sprint.EndUtc!.Value;
        var service = new SprintSpilloverService();
        var iterationMoveTimestamp = sprintEnd.AddHours(1);
        var workItemsById = new Dictionary<int, WorkItemSnapshot>
        {
            [101] = new(101, WorkItemType.Pbi, "Active", "\\Project\\Sprint 2")
        };
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
        {
            [101] =
            [
                CreateFieldChangeEvent(101, "System.IterationPath", iterationMoveTimestamp, sprint.Path, "\\Project\\Sprint 2")
            ]
        };

        var spillover = service.DetectSpillover(
            new HashSet<int> { 101 },
            workItemsById,
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(),
            iterationEventsByWorkItem,
            stateLookup: null,
            sprint,
            nextSprintPath: "\\Project\\Sprint 2",
            sprintEnd);

        Assert.HasCount(1, spillover);
        Assert.AreEqual(iterationMoveTimestamp, spillover[0].SpilloverAt);
    }

    [TestMethod]
    public void SprintFactService_BuildSprintFactResult_ReturnsCanonicalSprintTotals()
    {
        var sprint = CreateSprint();
        var service = new SprintFactService(
            new SprintCommitmentService(),
            new SprintScopeChangeService(),
            new SprintCompletionService(),
            new SprintSpilloverService(),
            new CanonicalStoryPointResolutionService());
        var commitmentTimestamp = SprintCommitmentLookup.GetCommitmentTimestamp(sprint.StartUtc!.Value);
        var sprintEnd = sprint.EndUtc!.Value;
        var nextSprintPath = "\\Project\\Sprint 2";

        IReadOnlyDictionary<int, CanonicalWorkItem> canonicalWorkItemsById = new Dictionary<int, CanonicalWorkItem>
        {
            [101] = new(101, WorkItemType.Pbi, 900, null, 5),
            [102] = new(102, WorkItemType.Pbi, 901, null, 3),
            [103] = new(103, WorkItemType.Pbi, 902, null, 2),
            [104] = new(104, WorkItemType.Pbi, 903, null, 8),
            [105] = new(105, WorkItemType.Bug, 904, null, 13)
        };
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById = new Dictionary<int, WorkItemSnapshot>
        {
            [101] = new(101, WorkItemType.Pbi, "Resolved", sprint.Path),
            [102] = new(102, WorkItemType.Pbi, "Resolved", sprint.Path),
            [103] = new(103, WorkItemType.Pbi, "Active", "\\Project\\Backlog"),
            [104] = new(104, WorkItemType.Pbi, "Active", nextSprintPath),
            [105] = new(105, WorkItemType.Bug, "Resolved", sprint.Path)
        };
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
        {
            [102] =
            [
                CreateFieldChangeEvent(102, "System.IterationPath", commitmentTimestamp.AddHours(2), "\\Project\\Backlog", sprint.Path)
            ],
            [103] =
            [
                CreateFieldChangeEvent(103, "System.IterationPath", commitmentTimestamp.AddHours(3), sprint.Path, "\\Project\\Backlog")
            ],
            [104] =
            [
                CreateFieldChangeEvent(104, "System.IterationPath", sprintEnd.AddHours(1), sprint.Path, nextSprintPath)
            ]
        };
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
        {
            [101] =
            [
                CreateFieldChangeEvent(101, "System.State", sprint.StartUtc.Value.AddDays(2), "Active", "Resolved")
            ],
            [102] =
            [
                CreateFieldChangeEvent(102, "System.State", sprint.StartUtc.Value.AddDays(4), "Active", "Resolved")
            ],
            [105] =
            [
                CreateFieldChangeEvent(105, "System.State", sprint.StartUtc.Value.AddDays(5), "Active", "Resolved")
            ]
        };
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification> stateLookup =
            new Dictionary<(string WorkItemType, string StateName), StateClassification>
            {
                [(WorkItemType.Pbi, "Resolved")] = StateClassification.Done,
                [(WorkItemType.Bug, "Resolved")] = StateClassification.Done
            };

        var result = service.BuildSprintFactResult(
            sprint,
            canonicalWorkItemsById,
            workItemsById,
            iterationEventsByWorkItem,
            stateEventsByWorkItem,
            stateLookup,
            nextSprintPath);

        Assert.AreEqual(15d, result.CommittedStoryPoints, 0.001);
        Assert.AreEqual(3d, result.AddedStoryPoints, 0.001);
        Assert.AreEqual(2d, result.RemovedStoryPoints, 0.001);
        Assert.AreEqual(8d, result.DeliveredStoryPoints, 0.001);
        Assert.AreEqual(3d, result.DeliveredFromAddedStoryPoints, 0.001);
        Assert.AreEqual(8d, result.SpilloverStoryPoints, 0.001);
        Assert.AreEqual(8d, result.RemainingStoryPoints, 0.001);
    }

    private static SprintDefinition CreateSprint()
    {
        return new SprintDefinition(
            1,
            10,
            "\\Project\\Sprint 1",
            "Sprint 1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero));
    }

    private static FieldChangeEvent CreateFieldChangeEvent(
        int workItemId,
        string fieldRefName,
        DateTimeOffset timestamp,
        string? oldValue,
        string? newValue)
    {
        return new FieldChangeEvent(
            1,
            workItemId,
            1,
            fieldRefName,
            timestamp,
            timestamp.UtcDateTime,
            oldValue,
            newValue);
    }
}
