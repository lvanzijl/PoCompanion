using PoTool.Core.Domain.Cdc.Sprints;
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
