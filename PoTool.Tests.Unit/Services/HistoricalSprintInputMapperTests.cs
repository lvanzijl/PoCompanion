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

    [TestMethod]
    public void ToCanonicalWorkItem_MapsWorkItemEntityToMinimalMetricsDomainInput()
    {
        var entity = new WorkItemEntity
        {
            TfsId = 101,
            Type = WorkItemType.Pbi,
            ParentTfsId = 55,
            BusinessValue = 8,
            StoryPoints = 5
        };

        var workItem = entity.ToCanonicalWorkItem();

        Assert.AreEqual(101, workItem.WorkItemId);
        Assert.AreEqual(WorkItemType.Pbi, workItem.WorkItemType);
        Assert.AreEqual(55, workItem.ParentWorkItemId);
        Assert.AreEqual(8, workItem.BusinessValue);
        Assert.AreEqual(5, workItem.StoryPoints);
    }

    [TestMethod]
    public void ToCanonicalWorkItem_MapsWorkItemDtoToMinimalMetricsDomainInput()
    {
        var dto = new WorkItemDto(
            TfsId: 202,
            Type: WorkItemType.Feature,
            Title: "Feature",
            ParentTfsId: 11,
            AreaPath: "Area",
            IterationPath: "Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: 3,
            Description: null,
            BusinessValue: 13,
            BacklogPriority: null,
            StoryPoints: null);

        var workItem = dto.ToCanonicalWorkItem();

        Assert.AreEqual(202, workItem.WorkItemId);
        Assert.AreEqual(WorkItemType.Feature, workItem.WorkItemType);
        Assert.AreEqual(11, workItem.ParentWorkItemId);
        Assert.AreEqual(13, workItem.BusinessValue);
        Assert.IsNull(workItem.StoryPoints);
    }
}
