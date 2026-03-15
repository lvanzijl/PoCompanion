using PoTool.Api.Adapters;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.Models;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

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
