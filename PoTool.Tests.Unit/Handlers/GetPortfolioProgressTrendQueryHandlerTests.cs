using PoTool.Api.Handlers.Metrics;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Unit tests for GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort.
///
/// These tests verify the event-replay algorithm that reconstructs total backlog scope
/// at the end of each sprint by undoing future changes to the ActivityEventLedger.
/// </summary>
[TestClass]
public class GetPortfolioProgressTrendQueryHandlerTests
{
    private static readonly DateTimeOffset Sprint1End = new(2026, 1, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sprint2End = new(2026, 1, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sprint3End = new(2026, 2, 11, 0, 0, 0, TimeSpan.Zero);

    // Helper to create PbiSnapshot
    private static GetPortfolioProgressTrendQueryHandler.PbiSnapshot Pbi(
        int id, int? effort, string state = "Active", DateTimeOffset? createdDate = null)
        => new()
        {
            TfsId = id,
            Effort = effort,
            State = state,
            CreatedDate = createdDate ?? Sprint1End.AddDays(-30)
        };

    // Helper to create ScopeEvent for effort changes
    private static GetPortfolioProgressTrendQueryHandler.ScopeEvent EffortChange(
        int workItemId, DateTimeOffset timestamp, string? oldValue, string? newValue)
        => new()
        {
            WorkItemId = workItemId,
            FieldRefName = "Microsoft.VSTS.Scheduling.Effort",
            EventTimestamp = timestamp,
            OldValue = oldValue,
            NewValue = newValue
        };

    // Helper to create ScopeEvent for state changes
    private static GetPortfolioProgressTrendQueryHandler.ScopeEvent StateChange(
        int workItemId, DateTimeOffset timestamp, string? newValue)
        => new()
        {
            WorkItemId = workItemId,
            FieldRefName = "System.State",
            EventTimestamp = timestamp,
            OldValue = "Active",
            NewValue = newValue
        };

    [TestMethod]
    public void ComputeHistoricalScopeEffort_NoEvents_ReturnsCurrentEffort()
    {
        // Arrange: 2 PBIs, no activity events
        var pbis = new[] { 1, 2 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 10),
            [2] = Pbi(2, effort: 20),
        };

        // Act
        var scope = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details,
            effortEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>(),
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        // Assert: when no events exist, current effort is used as best-effort approximation
        Assert.AreEqual(30.0, scope, "No events: current effort sum should be returned");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_UndoesFutureEffortChange()
    {
        // Arrange: PBI had effort 10 in sprint 1, then changed to 20 in sprint 2
        // Current (sprint 3 end): effort = 20
        var pbis = new[] { 1 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 20)
        };
        var effortEvents = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [1] = new()
            {
                // Changed from 10 to 20 during sprint 2 (after sprint 1 end)
                EffortChange(1, Sprint1End.AddDays(10), oldValue: "10", newValue: "20")
            }
        };

        // Act: historical scope at end of Sprint 1 (before the change)
        var scopeAtSprint1 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details, effortEvents,
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        // Act: historical scope at end of Sprint 2 (after the change)
        var scopeAtSprint2 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint2End, pbis, details, effortEvents,
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        Assert.AreEqual(10.0, scopeAtSprint1, "Scope at Sprint 1 end: 20 - (20-10) = 10");
        Assert.AreEqual(20.0, scopeAtSprint2, "Scope at Sprint 2 end: no future changes, = 20");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_UndoesMultipleFutureChanges()
    {
        // Arrange: PBI effort changed multiple times
        // Sprint1 end: effort was 5 (original estimate)
        // Sprint2 end: effort changed to 10
        // Sprint3 end (current): effort = 15
        var pbis = new[] { 1 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 15)
        };
        var effortEvents = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [1] = new()
            {
                EffortChange(1, Sprint1End.AddDays(5), oldValue: "5", newValue: "10"),  // sprint 1→2
                EffortChange(1, Sprint2End.AddDays(5), oldValue: "10", newValue: "15"), // sprint 2→3
            }
        };

        var scopeAtSprint1 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details, effortEvents,
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        var scopeAtSprint2 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint2End, pbis, details, effortEvents,
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        // Sprint1: undo both: 15 + (5-10) + (10-15) = 15 - 5 - 5 = 5
        Assert.AreEqual(5.0, scopeAtSprint1, "Scope at Sprint 1: both future changes undone");
        // Sprint2: undo only sprint 2→3 change: 15 + (10-15) = 10
        Assert.AreEqual(10.0, scopeAtSprint2, "Scope at Sprint 2: only one future change undone");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_ExcludesItemsCreatedAfterSprintEnd()
    {
        // Arrange: PBI 1 existed at sprint 1 end; PBI 2 was created later
        var pbis = new[] { 1, 2 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 10, createdDate: Sprint1End.AddDays(-5)),    // existed before sprint 1 end
            [2] = Pbi(2, effort: 20, createdDate: Sprint1End.AddDays(1)),     // created after sprint 1 end
        };

        var scopeAtSprint1 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details,
            effortEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>(),
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        var scopeAtSprint2 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint2End, pbis, details,
            effortEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>(),
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        Assert.AreEqual(10.0, scopeAtSprint1, "Sprint 1: only PBI 1 existed; PBI 2 excluded");
        Assert.AreEqual(30.0, scopeAtSprint2, "Sprint 2: both PBIs existed");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_ExcludesRemovedItemsAtSprintEnd()
    {
        // Arrange: PBI 1 was moved to Removed during sprint 1
        var pbis = new[] { 1, 2 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 10, state: "Removed"),  // currently Removed
            [2] = Pbi(2, effort: 20),
        };
        var stateEvents = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [1] = new()
            {
                // Was removed within sprint 1 (before sprint 1 end)
                StateChange(1, Sprint1End.AddDays(-2), newValue: "Removed")
            }
        };

        var scopeAtSprint1 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details,
            effortEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>(),
            stateEventsByItem: stateEvents);

        Assert.AreEqual(20.0, scopeAtSprint1, "PBI 1 was already Removed at sprint 1 end; excluded");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_IncludesItemsRemovedAfterSprintEnd()
    {
        // Arrange: PBI 1 is currently Removed but was removed AFTER sprint 1 end
        var pbis = new[] { 1, 2 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 10, state: "Removed"),  // currently Removed
            [2] = Pbi(2, effort: 20),
        };
        var stateEvents = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [1] = new()
            {
                // Was removed after sprint 1 end (in sprint 2)
                StateChange(1, Sprint1End.AddDays(5), newValue: "Removed")
            }
        };

        var scopeAtSprint1 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details,
            effortEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>(),
            stateEventsByItem: stateEvents);

        Assert.AreEqual(30.0, scopeAtSprint1,
            "PBI 1 was NOT yet removed at sprint 1 end; should be included in scope");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_HandlesNullEffortValues()
    {
        // Arrange: PBI with no effort (null → 0)
        var pbis = new[] { 1 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: null)
        };

        var scope = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details,
            effortEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>(),
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        Assert.AreEqual(0.0, scope, "Null effort should be treated as 0");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_HandlesNullEventValues()
    {
        // Arrange: Effort changed from null (= 0) to 20 — undoing should give back 0
        var pbis = new[] { 1 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 20)
        };
        var effortEvents = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [1] = new()
            {
                // Effort was set from null to 20 after sprint 1 end
                EffortChange(1, Sprint1End.AddDays(1), oldValue: null, newValue: "20")
            }
        };

        var scopeAtSprint1 = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details, effortEvents,
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        // historical = 20 + (0 - 20) = 0
        Assert.AreEqual(0.0, scopeAtSprint1, "Undoing effort set-from-null should result in 0");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_ClampsNegativeToZero()
    {
        // Arrange: data inconsistency (negative reconstructed effort) should be clamped to 0
        var pbis = new[] { 1 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 5)
        };
        var effortEvents = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [1] = new()
            {
                // Effort went from 20 to 5 after sprint 1; reconstructing: 5 + (20 - 5) = 20... no.
                // Let's force a scenario: a decrease from 5 to 0, and we check sprint before:
                // Actually to get negative: current=5, event after sprint1: old=0, new=5
                // Undo: 5 + (0 - 5) = 0 → not negative
                // For truly negative: current=0, event after sprint1: old=10, new=0
                EffortChange(1, Sprint1End.AddDays(1), oldValue: "0", newValue: "10")
                // current=10, historical = 10 + (0 - 10) = 0; not negative either
            }
        };

        // To hit negative: current=5, future event: old=null(0), new=10 → 5 + (0-10) = -5 → clamp to 0
        var effortEvents2 = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [1] = new()
            {
                EffortChange(1, Sprint1End.AddDays(1), oldValue: null, newValue: "10")
            }
        };

        var scope = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, details, effortEvents2,
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        // 5 + (0 - 10) = -5 → clamped to 0
        Assert.AreEqual(0.0, scope, "Negative reconstructed effort should be clamped to 0");
    }

    [TestMethod]
    public void ComputeHistoricalScopeEffort_SumsTotalScopeAcrossMultiplePbis()
    {
        // Arrange: 3 PBIs with different histories
        var pbis = new[] { 1, 2, 3 };
        var details = new Dictionary<int, GetPortfolioProgressTrendQueryHandler.PbiSnapshot>
        {
            [1] = Pbi(1, effort: 10),  // unchanged
            [2] = Pbi(2, effort: 20),  // changed after sprint 1: was 15
            [3] = Pbi(3, effort: 5),   // created after sprint 1: excluded
        };
        var effortEvents = new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>
        {
            [2] = new()
            {
                EffortChange(2, Sprint1End.AddDays(3), oldValue: "15", newValue: "20")
            }
        };
        var pbi3CreatedAfterSprint1 = details.ToDictionary(
            k => k.Key,
            v => v.Key == 3
                ? new GetPortfolioProgressTrendQueryHandler.PbiSnapshot
                    { TfsId = 3, Effort = 5, State = "Active", CreatedDate = Sprint1End.AddDays(1) }
                : v.Value);

        var scope = GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort(
            Sprint1End, pbis, pbi3CreatedAfterSprint1, effortEvents,
            stateEventsByItem: new Dictionary<int, List<GetPortfolioProgressTrendQueryHandler.ScopeEvent>>());

        // PBI 1: 10, PBI 2: 20 + (15-20) = 15, PBI 3: excluded (created after sprint 1)
        Assert.AreEqual(25.0, scope, "Sprint 1: PBI1(10) + PBI2(15) = 25; PBI3 excluded");
    }
}
