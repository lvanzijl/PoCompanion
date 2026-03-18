namespace PoTool.Core.Contracts;

/// <summary>
/// Plans incremental work-item sync decisions from pure graph facts.
/// </summary>
public interface IIncrementalSyncPlanner
{
    /// <summary>
    /// Produces a deterministic sync plan for the supplied request.
    /// </summary>
    IncrementalSyncPlan Plan(IncrementalSyncPlannerRequest request);
}

/// <summary>
/// Pure request model for incremental sync planning.
/// </summary>
public sealed class IncrementalSyncPlannerRequest
{
    public IReadOnlyList<int> RootIds { get; init; } = [];

    public IReadOnlyList<int> PreviousAnalyticalScopeIds { get; init; } = [];

    public IReadOnlyList<int> PreviousClosureScopeIds { get; init; } = [];

    public IReadOnlyDictionary<int, int?> PreviousParentById { get; init; } = new Dictionary<int, int?>();

    public IReadOnlyList<int> CurrentAnalyticalScopeIds { get; init; } = [];

    public IReadOnlyList<int> CurrentClosureScopeIds { get; init; } = [];

    public IReadOnlyDictionary<int, int?> CurrentParentById { get; init; } = new Dictionary<int, int?>();

    public IReadOnlyList<int> ChangedIdsSinceWatermark { get; init; } = [];

    public bool ForceFullHydration { get; init; }
}

/// <summary>
/// Deterministic result model for incremental sync planning.
/// </summary>
public sealed class IncrementalSyncPlan
{
    public IncrementalSyncPlanningMode PlanningMode { get; init; }

    public IReadOnlyList<int> AnalyticalScopeIds { get; init; } = [];

    public IReadOnlyList<int> ClosureScopeIds { get; init; } = [];

    public IReadOnlyList<int> EnteredAnalyticalScopeIds { get; init; } = [];

    public IReadOnlyList<int> LeftAnalyticalScopeIds { get; init; } = [];

    public IReadOnlyList<int> EnteredClosureScopeIds { get; init; } = [];

    public IReadOnlyList<int> LeftClosureScopeIds { get; init; } = [];

    public IReadOnlyList<int> HierarchyChangedIds { get; init; } = [];

    public IReadOnlyList<int> IdsToHydrate { get; init; } = [];

    public bool RequiresRelationshipSnapshotRebuild { get; init; }

    public bool RequiresResolutionRebuild { get; init; }

    public bool RequiresProjectionRefresh { get; init; }

    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
}

/// <summary>
/// Planning mode emitted by the incremental sync planner.
/// </summary>
public enum IncrementalSyncPlanningMode
{
    Incremental = 0,
    Full = 1
}

/// <summary>
/// Stable reason codes emitted by the incremental sync planner.
/// </summary>
public static class IncrementalSyncPlannerReasonCodes
{
    public const string FullHydrationRequested = nameof(FullHydrationRequested);
    public const string EnteredClosureScope = nameof(EnteredClosureScope);
    public const string LeftAnalyticalScope = nameof(LeftAnalyticalScope);
    public const string ChangedSinceWatermark = nameof(ChangedSinceWatermark);
    public const string ParentChanged = nameof(ParentChanged);
    public const string HierarchyMembershipChanged = nameof(HierarchyMembershipChanged);
}
