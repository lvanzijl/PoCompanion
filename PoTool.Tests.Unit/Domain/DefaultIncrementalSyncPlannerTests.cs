using PoTool.Core.Contracts;
using PoTool.Core.Sync;

namespace PoTool.Tests.Unit.Domain;

[TestClass]
public sealed class DefaultIncrementalSyncPlannerTests
{
    private static readonly IIncrementalSyncPlanner Planner = new DefaultIncrementalSyncPlanner();

    [TestMethod]
    public void Plan_FirstSync_ProducesFullPlan()
    {
        var plan = Planner.Plan(new IncrementalSyncPlannerRequest
        {
            RootIds = [100],
            CurrentAnalyticalScopeIds = [100, 101],
            CurrentClosureScopeIds = [100, 101, 900],
            CurrentParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [900] = null
            },
            ForceFullHydration = true
        });

        Assert.AreEqual(IncrementalSyncPlanningMode.Full, plan.PlanningMode);
        CollectionAssert.AreEqual(new[] { 100, 101 }, plan.EnteredAnalyticalScopeIds.ToArray());
        CollectionAssert.AreEqual(new[] { 100, 101, 900 }, plan.EnteredClosureScopeIds.ToArray());
        CollectionAssert.AreEqual(new[] { 100, 101, 900 }, plan.IdsToHydrate.ToArray());
        Assert.IsTrue(plan.RequiresRelationshipSnapshotRebuild);
        Assert.IsTrue(plan.RequiresResolutionRebuild);
        Assert.IsTrue(plan.RequiresProjectionRefresh);
        CollectionAssert.Contains(plan.ReasonCodes.ToList(), IncrementalSyncPlannerReasonCodes.FullHydrationRequested);
    }

    [TestMethod]
    public void Plan_NewItemInScope_HydratesEnteredItem()
    {
        var plan = Planner.Plan(new IncrementalSyncPlannerRequest
        {
            RootIds = [100],
            PreviousAnalyticalScopeIds = [100, 101],
            PreviousClosureScopeIds = [100, 101, 900],
            PreviousParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [900] = null
            },
            CurrentAnalyticalScopeIds = [100, 101, 102],
            CurrentClosureScopeIds = [100, 101, 102, 900],
            CurrentParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [102] = 100,
                [900] = null
            },
            ChangedIdsSinceWatermark = [102]
        });

        CollectionAssert.AreEqual(new[] { 102 }, plan.EnteredAnalyticalScopeIds.ToArray());
        CollectionAssert.AreEqual(new[] { 102 }, plan.EnteredClosureScopeIds.ToArray());
        CollectionAssert.AreEqual(new[] { 102 }, plan.IdsToHydrate.ToArray());
        CollectionAssert.AreEqual(new[] { 102 }, plan.HierarchyChangedIds.ToArray());
        Assert.IsTrue(plan.RequiresRelationshipSnapshotRebuild);
        Assert.IsTrue(plan.RequiresResolutionRebuild);
    }

    [TestMethod]
    public void Plan_ItemMovedIntoScope_ByReparenting_IsMarkedAsHierarchyChange()
    {
        var plan = Planner.Plan(new IncrementalSyncPlannerRequest
        {
            RootIds = [100],
            PreviousAnalyticalScopeIds = [100, 101],
            PreviousClosureScopeIds = [100, 101, 900],
            PreviousParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [900] = null
            },
            CurrentAnalyticalScopeIds = [100, 101, 200],
            CurrentClosureScopeIds = [100, 101, 200, 900],
            CurrentParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [200] = 100,
                [900] = null
            },
            ChangedIdsSinceWatermark = [200]
        });

        CollectionAssert.AreEqual(new[] { 200 }, plan.EnteredAnalyticalScopeIds.ToArray());
        CollectionAssert.AreEqual(new[] { 200 }, plan.HierarchyChangedIds.ToArray());
        CollectionAssert.AreEqual(new[] { 200 }, plan.IdsToHydrate.ToArray());
    }

    [TestMethod]
    public void Plan_ItemMovedOutOfScope_TracksExplicitExitWithoutHydration()
    {
        var plan = Planner.Plan(new IncrementalSyncPlannerRequest
        {
            RootIds = [100],
            PreviousAnalyticalScopeIds = [100, 101, 200],
            PreviousClosureScopeIds = [100, 101, 200, 900],
            PreviousParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [200] = 100,
                [900] = null
            },
            CurrentAnalyticalScopeIds = [100, 101],
            CurrentClosureScopeIds = [100, 101, 900],
            CurrentParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [900] = null
            }
        });

        CollectionAssert.AreEqual(new[] { 200 }, plan.LeftAnalyticalScopeIds.ToArray());
        CollectionAssert.AreEqual(new[] { 200 }, plan.LeftClosureScopeIds.ToArray());
        CollectionAssert.AreEqual(new[] { 200 }, plan.HierarchyChangedIds.ToArray());
        Assert.IsEmpty(plan.IdsToHydrate);
        Assert.IsTrue(plan.RequiresRelationshipSnapshotRebuild);
        Assert.IsTrue(plan.RequiresResolutionRebuild);
        Assert.IsTrue(plan.RequiresProjectionRefresh);
    }

    [TestMethod]
    public void Plan_ParentChangeInsideScope_HydratesChangedNode()
    {
        var plan = Planner.Plan(new IncrementalSyncPlannerRequest
        {
            RootIds = [100],
            PreviousAnalyticalScopeIds = [100, 101, 102],
            PreviousClosureScopeIds = [100, 101, 102, 900, 901],
            PreviousParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [102] = 100,
                [900] = null,
                [901] = null
            },
            CurrentAnalyticalScopeIds = [100, 101, 102],
            CurrentClosureScopeIds = [100, 101, 102, 900, 901],
            CurrentParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [102] = 101,
                [900] = null,
                [901] = null
            }
        });

        CollectionAssert.AreEqual(new[] { 102 }, plan.HierarchyChangedIds.ToArray());
        CollectionAssert.AreEqual(new[] { 102 }, plan.IdsToHydrate.ToArray());
        Assert.IsTrue(plan.RequiresRelationshipSnapshotRebuild);
        Assert.IsTrue(plan.RequiresResolutionRebuild);
    }

    [TestMethod]
    public void Plan_FieldOnlyChange_HydratesChangedItemWithoutHierarchyInvalidation()
    {
        var plan = Planner.Plan(new IncrementalSyncPlannerRequest
        {
            RootIds = [100],
            PreviousAnalyticalScopeIds = [100, 101],
            PreviousClosureScopeIds = [100, 101, 900],
            PreviousParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [900] = null
            },
            CurrentAnalyticalScopeIds = [100, 101],
            CurrentClosureScopeIds = [100, 101, 900],
            CurrentParentById = new Dictionary<int, int?>
            {
                [100] = 900,
                [101] = 100,
                [900] = null
            },
            ChangedIdsSinceWatermark = [101]
        });

        Assert.IsEmpty(plan.HierarchyChangedIds);
        CollectionAssert.AreEqual(new[] { 101 }, plan.IdsToHydrate.ToArray());
        Assert.IsFalse(plan.RequiresRelationshipSnapshotRebuild);
        Assert.IsFalse(plan.RequiresResolutionRebuild);
        Assert.IsTrue(plan.RequiresProjectionRefresh);
        CollectionAssert.Contains(plan.ReasonCodes.ToList(), IncrementalSyncPlannerReasonCodes.ChangedSinceWatermark);
    }
}
