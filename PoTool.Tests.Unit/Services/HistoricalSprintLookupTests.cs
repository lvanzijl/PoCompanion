using PoTool.Core.Domain.Sprints;
using PoTool.Core.Metrics.Models;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class HistoricalSprintLookupTests
{
    [TestMethod]
    public void BuildCommittedWorkItemIds_ReconstructsIterationFromDomainInputs()
    {
        var commitmentTimestamp = new DateTimeOffset(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        var workItemsById = new Dictionary<int, WorkItemSnapshot>
        {
            [101] = new(101, WorkItemType.Pbi, "Active", "\\Project\\Backlog")
        };
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
        {
            [101] =
            [
                new FieldChangeEvent(
                    EventId: 1,
                    WorkItemId: 101,
                    UpdateId: 10,
                    FieldRefName: "System.IterationPath",
                    Timestamp: commitmentTimestamp.AddHours(2),
                    TimestampUtc: commitmentTimestamp.AddHours(2).UtcDateTime,
                    OldValue: "\\Project\\Sprint 1",
                    NewValue: "\\Project\\Backlog")
            ]
        };

        var committedWorkItemIds = SprintCommitmentLookup.BuildCommittedWorkItemIds(
            workItemsById,
            iterationEventsByWorkItem,
            "\\Project\\Sprint 1",
            commitmentTimestamp);

        CollectionAssert.AreEquivalent(new[] { 101 }, committedWorkItemIds.ToArray());
    }

    [TestMethod]
    public void Build_FirstDoneDelivery_UsesMappedFieldChangeEvents()
    {
        var firstDoneTimestamp = new DateTimeOffset(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        var workItemsById = new Dictionary<int, WorkItemSnapshot>
        {
            [101] = new(101, WorkItemType.Pbi, "Resolved", "\\Project\\Sprint 1")
        };
        var stateLookup = StateClassificationLookup.Create(
        [
            new WorkItemStateClassificationDto
            {
                WorkItemType = WorkItemType.Pbi,
                StateName = "Active",
                Classification = StateClassification.InProgress
            },
            new WorkItemStateClassificationDto
            {
                WorkItemType = WorkItemType.Pbi,
                StateName = "Resolved",
                Classification = StateClassification.Done
            }
        ]);
        IReadOnlyList<FieldChangeEvent> stateEvents =
        [
            new FieldChangeEvent(1, 101, 1, "System.State", firstDoneTimestamp, firstDoneTimestamp.UtcDateTime, "Active", "Resolved"),
            new FieldChangeEvent(2, 101, 2, "System.State", firstDoneTimestamp.AddDays(1), firstDoneTimestamp.AddDays(1).UtcDateTime, "Resolved", "Active"),
            new FieldChangeEvent(3, 101, 3, "System.State", firstDoneTimestamp.AddDays(2), firstDoneTimestamp.AddDays(2).UtcDateTime, "Active", "Resolved")
        ];

        var firstDoneByWorkItem = FirstDoneDeliveryLookup.Build(stateEvents, workItemsById, stateLookup);

        Assert.AreEqual(firstDoneTimestamp, firstDoneByWorkItem[101]);
    }

    [TestMethod]
    public void GetClassification_UsesCanonicalStateMappings()
    {
        var stateLookup = StateClassificationLookup.Create(
        [
            new WorkItemStateClassificationDto
            {
                WorkItemType = WorkItemType.Pbi,
                StateName = "Done",
                Classification = StateClassification.Done
            }
        ]);

        var classification = StateClassificationLookup.GetClassification(stateLookup, WorkItemType.Pbi, "done");

        Assert.AreEqual(StateClassification.Done, classification);
    }

    [TestMethod]
    public void GetStateAtTimestamp_ReconstructsHistoricalStateFromLaterEvents()
    {
        var targetTimestamp = new DateTimeOffset(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        IReadOnlyList<FieldChangeEvent> stateEvents =
        [
            new FieldChangeEvent(
                EventId: 1,
                WorkItemId: 101,
                UpdateId: 10,
                FieldRefName: "System.State",
                Timestamp: targetTimestamp.AddHours(1),
                TimestampUtc: targetTimestamp.AddHours(1).UtcDateTime,
                OldValue: "Active",
                NewValue: "Resolved")
        ];

        var stateAtTimestamp = StateReconstructionLookup.GetStateAtTimestamp("Resolved", stateEvents, targetTimestamp);

        Assert.AreEqual("Active", stateAtTimestamp);
    }

    [TestMethod]
    public void BuildSpilloverWorkItemIds_UsesMappedSnapshotsAndSprintDefinitions()
    {
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));
        var sprint = new SprintDefinition(1, 10, "\\Project\\Sprint 1", "Sprint 1", sprintEnd.AddDays(-13), sprintEnd);
        var workItemsById = new Dictionary<int, WorkItemSnapshot>
        {
            [101] = new(101, WorkItemType.Pbi, "Active", "\\Project\\Sprint 2")
        };
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
        {
            [101] =
            [
                new FieldChangeEvent(
                    EventId: 1,
                    WorkItemId: 101,
                    UpdateId: 11,
                    FieldRefName: "System.IterationPath",
                    Timestamp: sprintEnd.AddHours(1),
                    TimestampUtc: sprintEnd.AddHours(1).UtcDateTime,
                    OldValue: "\\Project\\Sprint 1",
                    NewValue: "\\Project\\Sprint 2")
            ]
        };

        var spilloverWorkItemIds = SprintSpilloverLookup.BuildSpilloverWorkItemIds(
            new HashSet<int> { 101 },
            workItemsById,
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(),
            iterationEventsByWorkItem,
            stateLookup: null,
            sprint,
            nextSprintPath: "\\Project\\Sprint 2",
            sprintEnd);

        CollectionAssert.AreEquivalent(new[] { 101 }, spilloverWorkItemIds.ToArray());
    }
}
