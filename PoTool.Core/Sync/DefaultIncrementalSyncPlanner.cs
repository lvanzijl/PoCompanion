using PoTool.Core.Contracts;

namespace PoTool.Core.Sync;

/// <summary>
/// Default deterministic implementation of <see cref="IIncrementalSyncPlanner"/>.
/// </summary>
public sealed class DefaultIncrementalSyncPlanner : IIncrementalSyncPlanner
{
    public IncrementalSyncPlan Plan(IncrementalSyncPlannerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var previousAnalytical = CreateSortedSet(request.PreviousAnalyticalScopeIds);
        var previousClosure = CreateSortedSet(request.PreviousClosureScopeIds);
        var currentAnalytical = CreateSortedSet(request.CurrentAnalyticalScopeIds);
        var currentClosure = CreateSortedSet(request.CurrentClosureScopeIds);
        var changedIdsSinceWatermark = CreateSortedSet(request.ChangedIdsSinceWatermark);

        var enteredAnalytical = currentAnalytical.Except(previousAnalytical).ToArray();
        var leftAnalytical = previousAnalytical.Except(currentAnalytical).ToArray();
        var enteredClosure = currentClosure.Except(previousClosure).ToArray();
        var leftClosure = previousClosure.Except(currentClosure).ToArray();

        var parentChangedIds = previousClosure
            .Intersect(currentClosure)
            .Where(id => request.PreviousParentById.GetValueOrDefault(id) != request.CurrentParentById.GetValueOrDefault(id))
            .OrderBy(id => id)
            .ToArray();

        var hierarchyChangedIds = CreateSortedSet(
        [
            .. enteredAnalytical,
            .. leftAnalytical,
            .. enteredClosure,
            .. leftClosure,
            .. parentChangedIds
        ]).ToArray();

        var planningMode = request.ForceFullHydration
            ? IncrementalSyncPlanningMode.Full
            : IncrementalSyncPlanningMode.Incremental;

        var idsToHydrate = planningMode == IncrementalSyncPlanningMode.Full
            ? currentClosure.ToArray()
            : CreateSortedSet(
            [
                .. enteredClosure,
                .. parentChangedIds,
                .. changedIdsSinceWatermark.Intersect(currentClosure)
            ]).ToArray();

        var requiresRelationshipSnapshotRebuild = enteredClosure.Length > 0
            || leftClosure.Length > 0
            || hierarchyChangedIds.Length > 0;

        var requiresResolutionRebuild = enteredAnalytical.Length > 0
            || leftAnalytical.Length > 0
            || hierarchyChangedIds.Length > 0;

        var requiresProjectionRefresh = idsToHydrate.Length > 0
            || leftAnalytical.Length > 0
            || requiresResolutionRebuild;

        var reasonCodes = BuildReasonCodes(
            request.ForceFullHydration,
            enteredClosure,
            leftAnalytical,
            changedIdsSinceWatermark.Intersect(currentClosure).Any(),
            parentChangedIds,
            enteredAnalytical,
            leftClosure);

        return new IncrementalSyncPlan
        {
            PlanningMode = planningMode,
            AnalyticalScopeIds = currentAnalytical.ToArray(),
            ClosureScopeIds = currentClosure.ToArray(),
            EnteredAnalyticalScopeIds = enteredAnalytical,
            LeftAnalyticalScopeIds = leftAnalytical,
            EnteredClosureScopeIds = enteredClosure,
            LeftClosureScopeIds = leftClosure,
            HierarchyChangedIds = hierarchyChangedIds,
            IdsToHydrate = idsToHydrate,
            RequiresRelationshipSnapshotRebuild = requiresRelationshipSnapshotRebuild,
            RequiresResolutionRebuild = requiresResolutionRebuild,
            RequiresProjectionRefresh = requiresProjectionRefresh,
            ReasonCodes = reasonCodes
        };
    }

    private static string[] BuildReasonCodes(
        bool forceFullHydration,
        IReadOnlyCollection<int> enteredClosure,
        IReadOnlyCollection<int> leftAnalytical,
        bool hasChangedIdsInClosure,
        IReadOnlyCollection<int> parentChangedIds,
        IReadOnlyCollection<int> enteredAnalytical,
        IReadOnlyCollection<int> leftClosure)
    {
        var reasons = new List<string>(6);

        if (forceFullHydration)
        {
            reasons.Add(IncrementalSyncPlannerReasonCodes.FullHydrationRequested);
        }

        if (enteredClosure.Count > 0)
        {
            reasons.Add(IncrementalSyncPlannerReasonCodes.EnteredClosureScope);
        }

        if (leftAnalytical.Count > 0)
        {
            reasons.Add(IncrementalSyncPlannerReasonCodes.LeftAnalyticalScope);
        }

        if (hasChangedIdsInClosure)
        {
            reasons.Add(IncrementalSyncPlannerReasonCodes.ChangedSinceWatermark);
        }

        if (parentChangedIds.Count > 0)
        {
            reasons.Add(IncrementalSyncPlannerReasonCodes.ParentChanged);
        }

        if (enteredAnalytical.Count > 0 || leftAnalytical.Count > 0 || enteredClosure.Count > 0 || leftClosure.Count > 0)
        {
            reasons.Add(IncrementalSyncPlannerReasonCodes.HierarchyMembershipChanged);
        }

        return reasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToArray();
    }

    private static SortedSet<int> CreateSortedSet(IEnumerable<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new SortedSet<int>(values);
    }
}
