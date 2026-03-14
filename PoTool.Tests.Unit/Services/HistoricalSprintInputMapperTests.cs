using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Models;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class HistoricalSprintInputMapperTests
{
    [TestMethod]
    public void ToSnapshot_MapsWorkItemEntityToMinimalDomainInput()
    {
        var entity = new WorkItemEntity
        {
            TfsId = 101,
            Type = WorkItemType.Pbi,
            State = " Active ",
            IterationPath = " \\Project\\Sprint 1 "
        };

        var snapshot = entity.ToSnapshot();

        Assert.AreEqual(101, snapshot.WorkItemId);
        Assert.AreEqual(WorkItemType.Pbi, snapshot.WorkItemType);
        Assert.AreEqual("Active", snapshot.CurrentState);
        Assert.AreEqual("\\Project\\Sprint 1", snapshot.CurrentIterationPath);
    }

    [TestMethod]
    public void ToDefinition_MapsSprintEntityToMinimalDomainInput()
    {
        var start = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var end = start.AddDays(14);
        var entity = new SprintEntity
        {
            Id = 7,
            TeamId = 3,
            Path = "\\Project\\Sprint 7",
            Name = "Sprint 7",
            StartUtc = start,
            EndUtc = end
        };

        var definition = entity.ToDefinition();

        Assert.AreEqual(7, definition.SprintId);
        Assert.AreEqual(3, definition.TeamId);
        Assert.AreEqual("\\Project\\Sprint 7", definition.Path);
        Assert.AreEqual("Sprint 7", definition.Name);
        Assert.AreEqual(start, definition.StartUtc);
        Assert.AreEqual(end, definition.EndUtc);
    }

    [TestMethod]
    public void ToFieldChangeEvent_FallsBackToUtcTimestampWhenOffsetTimestampIsMissing()
    {
        var timestampUtc = new DateTime(2026, 1, 3, 12, 30, 0, DateTimeKind.Utc);
        var entity = new ActivityEventLedgerEntryEntity
        {
            Id = 11,
            WorkItemId = 101,
            UpdateId = 5,
            FieldRefName = "System.State",
            EventTimestampUtc = timestampUtc,
            OldValue = "Active",
            NewValue = "Resolved"
        };

        var fieldChange = entity.ToFieldChangeEvent();

        Assert.AreEqual(11, fieldChange.EventId);
        Assert.AreEqual(101, fieldChange.WorkItemId);
        Assert.AreEqual(5, fieldChange.UpdateId);
        Assert.AreEqual("System.State", fieldChange.FieldRefName);
        Assert.AreEqual(new DateTimeOffset(timestampUtc), fieldChange.Timestamp);
        Assert.AreEqual(timestampUtc, fieldChange.TimestampUtc);
        Assert.AreEqual("Active", fieldChange.OldValue);
        Assert.AreEqual("Resolved", fieldChange.NewValue);
    }
}
