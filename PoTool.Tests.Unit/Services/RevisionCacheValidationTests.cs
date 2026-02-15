using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Unit tests for CacheManagementService validation core logic:
/// - Revision replay to final state
/// - Normalization rules
/// - Diff generation
/// </summary>
[TestClass]
public class RevisionCacheValidationTests
{
    #region Normalization Tests

    [TestMethod]
    [Description("Null value normalizes to null")]
    public void Normalize_Null_ReturnsNull()
    {
        Assert.IsNull(CacheManagementService.Normalize(null));
    }

    [TestMethod]
    [Description("Empty string normalizes to null")]
    public void Normalize_Empty_ReturnsNull()
    {
        Assert.IsNull(CacheManagementService.Normalize(""));
    }

    [TestMethod]
    [Description("Whitespace-only string normalizes to null")]
    public void Normalize_Whitespace_ReturnsNull()
    {
        Assert.IsNull(CacheManagementService.Normalize("   "));
    }

    [TestMethod]
    [Description("Non-empty string is trimmed")]
    public void Normalize_ValueWithSpaces_ReturnsTrimmed()
    {
        Assert.AreEqual("Active", CacheManagementService.Normalize("  Active  "));
    }

    [TestMethod]
    [Description("Normal value is returned as-is")]
    public void Normalize_NormalValue_ReturnsUnchanged()
    {
        Assert.AreEqual("Active", CacheManagementService.Normalize("Active"));
    }

    #endregion

    #region BuildReplayedState Tests

    [TestMethod]
    [Description("BuildReplayedState extracts all whitelist fields from revision header")]
    public void BuildReplayedState_MapsAllFields()
    {
        // Arrange
        var revision = new RevisionHeaderEntity
        {
            WorkItemId = 42,
            RevisionNumber = 5,
            WorkItemType = "Bug",
            Title = "Fix the thing",
            State = "Active",
            Reason = "Moved to Active",
            IterationPath = @"Project\Sprint 1",
            AreaPath = @"Project\Team A",
            CreatedDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ChangedDate = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ChangedBy = "John Doe",
            ClosedDate = null,
            Effort = 3.0,
            Tags = "Tag1; Tag2",
            Severity = "2 - High"
        };

        // Act
        var state = CacheManagementService.BuildReplayedState(revision);

        // Assert
        Assert.AreEqual("42", state["System.Id"]);
        Assert.AreEqual("Bug", state["System.WorkItemType"]);
        Assert.AreEqual("Fix the thing", state["System.Title"]);
        Assert.AreEqual("Active", state["System.State"]);
        Assert.AreEqual("Moved to Active", state["System.Reason"]);
        Assert.AreEqual(@"Project\Sprint 1", state["System.IterationPath"]);
        Assert.AreEqual(@"Project\Team A", state["System.AreaPath"]);
        Assert.IsNotNull(state["System.CreatedDate"]);
        Assert.IsNotNull(state["System.ChangedDate"]);
        Assert.AreEqual("John Doe", state["System.ChangedBy"]);
        Assert.IsNull(state["Microsoft.VSTS.Common.ClosedDate"]);
        Assert.AreEqual("3", state["Microsoft.VSTS.Scheduling.Effort"]);
        Assert.AreEqual("Tag1; Tag2", state["System.Tags"]);
        Assert.AreEqual("2 - High", state["Microsoft.VSTS.Common.Severity"]);
    }

    [TestMethod]
    [Description("BuildReplayedState handles nullable fields as null")]
    public void BuildReplayedState_NullableFields_AreNull()
    {
        var revision = new RevisionHeaderEntity
        {
            WorkItemId = 1,
            RevisionNumber = 1,
            WorkItemType = "Task",
            Title = "A task",
            State = "New",
            IterationPath = "Path",
            AreaPath = "Area",
            ChangedDate = DateTimeOffset.UtcNow,
            // All nullable fields left as default null
        };

        var state = CacheManagementService.BuildReplayedState(revision);

        Assert.IsNull(state["System.Reason"]);
        Assert.IsNull(state["System.CreatedDate"]);
        Assert.IsNull(state["System.ChangedBy"]);
        Assert.IsNull(state["Microsoft.VSTS.Common.ClosedDate"]);
        Assert.IsNull(state["Microsoft.VSTS.Scheduling.Effort"]);
        Assert.IsNull(state["System.Tags"]);
        Assert.IsNull(state["Microsoft.VSTS.Common.Severity"]);
    }

    #endregion

    #region BuildCachedWorkItemState Tests

    [TestMethod]
    [Description("BuildCachedWorkItemState maps WorkItemEntity fields correctly")]
    public void BuildCachedWorkItemState_MapsFields()
    {
        var workItem = new WorkItemEntity
        {
            TfsId = 42,
            Type = "Bug",
            Title = "Fix the thing",
            State = "Active",
            IterationPath = @"Project\Sprint 1",
            AreaPath = @"Project\Team A",
            CreatedDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TfsChangedDate = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ClosedDate = null,
            Effort = 3,
            Tags = "Tag1; Tag2",
            Severity = "2 - High"
        };

        var state = CacheManagementService.BuildCachedWorkItemState(workItem);

        Assert.AreEqual("42", state["System.Id"]);
        Assert.AreEqual("Bug", state["System.WorkItemType"]);
        Assert.AreEqual("Fix the thing", state["System.Title"]);
        Assert.AreEqual("Active", state["System.State"]);
        Assert.AreEqual(@"Project\Sprint 1", state["System.IterationPath"]);
        Assert.AreEqual(@"Project\Team A", state["System.AreaPath"]);
        Assert.AreEqual("3", state["Microsoft.VSTS.Scheduling.Effort"]);
        Assert.AreEqual("Tag1; Tag2", state["System.Tags"]);
        Assert.AreEqual("2 - High", state["Microsoft.VSTS.Common.Severity"]);
    }

    #endregion

    #region CompareStates (Diff) Tests

    [TestMethod]
    [Description("Identical states produce no diffs")]
    public void CompareStates_IdenticalStates_NoDiffs()
    {
        var state = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1",
            ["System.WorkItemType"] = "Bug",
            ["System.Title"] = "Test",
            ["System.State"] = "Active",
            ["System.IterationPath"] = "Path",
            ["System.AreaPath"] = "Area",
            ["System.CreatedDate"] = "2025-01-01T00:00:00+00:00",
            ["System.ChangedDate"] = "2025-01-15T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = null,
            ["Microsoft.VSTS.Scheduling.Effort"] = "5",
            ["System.Tags"] = "Tag1",
            ["Microsoft.VSTS.Common.Severity"] = "3 - Medium"
        };

        var copy = new Dictionary<string, string?>(state, StringComparer.OrdinalIgnoreCase);
        var diffs = CacheManagementService.CompareStates(state, copy);

        Assert.IsEmpty(diffs);
    }

    [TestMethod]
    [Description("Different title produces a diff")]
    public void CompareStates_DifferentTitle_ProducesDiff()
    {
        var replayed = BuildMinimalState("Title A");
        var cached = BuildMinimalState("Title B");

        var diffs = CacheManagementService.CompareStates(replayed, cached);

        Assert.HasCount(1, diffs);
        Assert.AreEqual("System.Title", diffs[0].FieldName);
        Assert.AreEqual("Title A", diffs[0].ReplayedValue);
        Assert.AreEqual("Title B", diffs[0].RestValue);
    }

    [TestMethod]
    [Description("Null vs empty string is treated as equal (both normalize to null)")]
    public void CompareStates_NullVsEmpty_NoDiff()
    {
        var replayed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1", ["System.WorkItemType"] = "Bug",
            ["System.Title"] = "T", ["System.State"] = "New",
            ["System.IterationPath"] = "P", ["System.AreaPath"] = "A",
            ["System.CreatedDate"] = null, ["System.ChangedDate"] = "2025-01-01T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = null,
            ["Microsoft.VSTS.Scheduling.Effort"] = null,
            ["System.Tags"] = null, ["Microsoft.VSTS.Common.Severity"] = null
        };

        var cached = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1", ["System.WorkItemType"] = "Bug",
            ["System.Title"] = "T", ["System.State"] = "New",
            ["System.IterationPath"] = "P", ["System.AreaPath"] = "A",
            ["System.CreatedDate"] = "", ["System.ChangedDate"] = "2025-01-01T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = "", 
            ["Microsoft.VSTS.Scheduling.Effort"] = "  ",
            ["System.Tags"] = "", ["Microsoft.VSTS.Common.Severity"] = ""
        };

        var diffs = CacheManagementService.CompareStates(replayed, cached);
        Assert.IsEmpty(diffs);
    }

    [TestMethod]
    [Description("Multiple differences are all reported")]
    public void CompareStates_MultipleDiffs_AllReported()
    {
        var replayed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1", ["System.WorkItemType"] = "Bug",
            ["System.Title"] = "Old Title", ["System.State"] = "Active",
            ["System.IterationPath"] = "Path A", ["System.AreaPath"] = "Area",
            ["System.CreatedDate"] = null, ["System.ChangedDate"] = "2025-01-01T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = null,
            ["Microsoft.VSTS.Scheduling.Effort"] = "3",
            ["System.Tags"] = null, ["Microsoft.VSTS.Common.Severity"] = null
        };

        var cached = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1", ["System.WorkItemType"] = "Bug",
            ["System.Title"] = "New Title", ["System.State"] = "Done",
            ["System.IterationPath"] = "Path A", ["System.AreaPath"] = "Area",
            ["System.CreatedDate"] = null, ["System.ChangedDate"] = "2025-01-01T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = null,
            ["Microsoft.VSTS.Scheduling.Effort"] = "5",
            ["System.Tags"] = null, ["Microsoft.VSTS.Common.Severity"] = null
        };

        var diffs = CacheManagementService.CompareStates(replayed, cached);

        Assert.HasCount(3, diffs);
        Assert.IsTrue(diffs.Any(d => d.FieldName == "System.Title"));
        Assert.IsTrue(diffs.Any(d => d.FieldName == "System.State"));
        Assert.IsTrue(diffs.Any(d => d.FieldName == "Microsoft.VSTS.Scheduling.Effort"));
    }

    [TestMethod]
    [Description("Reason and ChangedBy fields are skipped in comparison")]
    public void CompareStates_SkipsReasonAndChangedBy()
    {
        var replayed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1", ["System.WorkItemType"] = "Bug",
            ["System.Title"] = "T", ["System.State"] = "New",
            ["System.Reason"] = "Created", ["System.ChangedBy"] = "User A",
            ["System.IterationPath"] = "P", ["System.AreaPath"] = "A",
            ["System.CreatedDate"] = null, ["System.ChangedDate"] = "2025-01-01T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = null,
            ["Microsoft.VSTS.Scheduling.Effort"] = null,
            ["System.Tags"] = null, ["Microsoft.VSTS.Common.Severity"] = null
        };

        var cached = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1", ["System.WorkItemType"] = "Bug",
            ["System.Title"] = "T", ["System.State"] = "New",
            ["System.Reason"] = "Different Reason", ["System.ChangedBy"] = "User B",
            ["System.IterationPath"] = "P", ["System.AreaPath"] = "A",
            ["System.CreatedDate"] = null, ["System.ChangedDate"] = "2025-01-01T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = null,
            ["Microsoft.VSTS.Scheduling.Effort"] = null,
            ["System.Tags"] = null, ["Microsoft.VSTS.Common.Severity"] = null
        };

        var diffs = CacheManagementService.CompareStates(replayed, cached);
        Assert.IsEmpty(diffs, "Reason and ChangedBy should be skipped");
    }

    #endregion

    #region Revision Timeline Tests

    [TestMethod]
    [Description("BuildRevisionTimelineEntries returns all field changes in revision order with normalized values")]
    public void BuildRevisionTimelineEntries_ReturnsOrderedTimeline()
    {
        var revisions = new List<RevisionHeaderEntity>
        {
            new()
            {
                RevisionNumber = 2,
                ChangedDate = new DateTimeOffset(2025, 1, 2, 10, 0, 0, TimeSpan.Zero),
                ChangedBy = "User B",
                FieldDeltas =
                {
                    new RevisionFieldDeltaEntity { Id = 2, FieldName = "System.State", OldValue = "New", NewValue = "Active" }
                }
            },
            new()
            {
                RevisionNumber = 1,
                ChangedDate = new DateTimeOffset(2025, 1, 1, 9, 0, 0, TimeSpan.Zero),
                ChangedBy = "User A",
                FieldDeltas =
                {
                    new RevisionFieldDeltaEntity { Id = 1, FieldName = "System.Title", OldValue = "  ", NewValue = "Created" }
                }
            }
        };

        var timeline = CacheManagementService.BuildRevisionTimelineEntries(revisions);

        Assert.HasCount(2, timeline);
        Assert.AreEqual(1, timeline[0].RevisionNumber);
        Assert.AreEqual("System.Title", timeline[0].FieldName);
        Assert.IsNull(timeline[0].OldValue);
        Assert.AreEqual("Created", timeline[0].NewValue);
        Assert.AreEqual("User A", timeline[0].ChangedBy);

        Assert.AreEqual(2, timeline[1].RevisionNumber);
        Assert.AreEqual("System.State", timeline[1].FieldName);
        Assert.AreEqual("New", timeline[1].OldValue);
        Assert.AreEqual("Active", timeline[1].NewValue);
    }

    [TestMethod]
    [Description("BuildRevisionTimelineEntries falls back to revision-header diffs when no field deltas are present")]
    public void BuildRevisionTimelineEntries_NoFieldDeltas_FallsBackToHeaderDiffs()
    {
        var revisions = new List<RevisionHeaderEntity>
        {
            new()
            {
                WorkItemId = 42,
                RevisionNumber = 1,
                WorkItemType = "Bug",
                Title = "Original title",
                State = "New",
                IterationPath = "Iteration 1",
                AreaPath = "Area 1",
                ChangedDate = new DateTimeOffset(2025, 1, 1, 9, 0, 0, TimeSpan.Zero)
            },
            new()
            {
                WorkItemId = 42,
                RevisionNumber = 2,
                WorkItemType = "Bug",
                Title = "Updated title",
                State = "Active",
                IterationPath = "Iteration 1",
                AreaPath = "Area 1",
                ChangedDate = new DateTimeOffset(2025, 1, 2, 10, 0, 0, TimeSpan.Zero)
            }
        };

        var timeline = CacheManagementService.BuildRevisionTimelineEntries(revisions);

        Assert.IsTrue(timeline.Any(change => change.RevisionNumber == 2 && change.FieldName == "System.Title"));
        Assert.IsTrue(timeline.Any(change => change.RevisionNumber == 2 && change.FieldName == "System.State"));
    }

    #endregion

    #region RevisionFieldWhitelist Tests

    [TestMethod]
    [Description("Whitelist contains expected fields")]
    public void RevisionFieldWhitelist_ContainsExpectedFields()
    {
        Assert.IsTrue(RevisionFieldWhitelist.Fields.Contains("System.Id"));
        Assert.IsTrue(RevisionFieldWhitelist.Fields.Contains("System.State"));
        Assert.IsTrue(RevisionFieldWhitelist.Fields.Contains("System.Title"));
        Assert.IsTrue(RevisionFieldWhitelist.Fields.Contains("Microsoft.VSTS.Scheduling.Effort"));
        Assert.IsTrue(RevisionFieldWhitelist.Fields.Contains("System.Tags"));
    }

    [TestMethod]
    [Description("Whitelist has exactly 14 fields")]
    public void RevisionFieldWhitelist_Has14Fields()
    {
        Assert.HasCount(14, RevisionFieldWhitelist.Fields);
    }

    #endregion

    #region Helpers

    private static Dictionary<string, string?> BuildMinimalState(string title)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "1",
            ["System.WorkItemType"] = "Bug",
            ["System.Title"] = title,
            ["System.State"] = "New",
            ["System.IterationPath"] = "Path",
            ["System.AreaPath"] = "Area",
            ["System.CreatedDate"] = null,
            ["System.ChangedDate"] = "2025-01-01T00:00:00+00:00",
            ["Microsoft.VSTS.Common.ClosedDate"] = null,
            ["Microsoft.VSTS.Scheduling.Effort"] = null,
            ["System.Tags"] = null,
            ["Microsoft.VSTS.Common.Severity"] = null
        };
    }

    #endregion
}
