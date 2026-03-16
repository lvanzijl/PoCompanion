using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using Metrics = PoTool.Core.Domain.Metrics;

namespace PoTool.Core.Domain.Cdc.Sprints;

/// <summary>
/// Reconstructs canonical sprint commitment membership.
/// </summary>
public interface ISprintCommitmentService
{
    /// <summary>
    /// Returns the canonical sprint commitment timestamp.
    /// </summary>
    DateTimeOffset GetCommitmentTimestamp(DateTimeOffset sprintStart);

    /// <summary>
    /// Builds the canonical committed work item IDs for the sprint.
    /// </summary>
    IReadOnlySet<int> BuildCommittedWorkItemIds(
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        string sprintPath,
        DateTimeOffset commitmentTimestamp);

    /// <summary>
    /// Builds canonical sprint commitment records for the sprint.
    /// </summary>
    IReadOnlyList<SprintCommitment> BuildCommitments(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem);
}

/// <summary>
/// Detects canonical sprint scope-add and scope-remove events.
/// </summary>
public interface ISprintScopeChangeService
{
    /// <summary>
    /// Detects work items added to the sprint after commitment.
    /// </summary>
    IReadOnlyList<SprintScopeAdded> DetectScopeAdded(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem);

    /// <summary>
    /// Detects work items removed from the sprint after commitment.
    /// </summary>
    IReadOnlyList<SprintScopeRemoved> DetectScopeRemoved(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem);
}

/// <summary>
/// Detects canonical sprint completions.
/// </summary>
public interface ISprintCompletionService
{
    /// <summary>
    /// Builds first-Done timestamps for work items available in the supplied snapshots.
    /// </summary>
    IReadOnlyDictionary<int, DateTimeOffset> BuildFirstDoneByWorkItem(
        IEnumerable<FieldChangeEvent> activityEvents,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null);

    /// <summary>
    /// Detects sprint completions inside the sprint window from first-Done timestamps.
    /// </summary>
    IReadOnlyList<SprintCompletion> DetectCompletions(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, DateTimeOffset> firstDoneByWorkItem);
}

/// <summary>
/// Detects canonical sprint spillover and adjacent sprint ordering.
/// </summary>
public interface ISprintSpilloverService
{
    /// <summary>
    /// Resolves the next sprint path for the same team.
    /// </summary>
    string? GetNextSprintPath(
        SprintDefinition sprint,
        IEnumerable<SprintDefinition> teamSprints);

    /// <summary>
    /// Builds committed spillover work item IDs.
    /// </summary>
    IReadOnlySet<int> BuildSpilloverWorkItemIds(
        IReadOnlySet<int> committedWorkItemIds,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        SprintDefinition sprint,
        string? nextSprintPath,
        DateTimeOffset sprintEnd);

    /// <summary>
    /// Detects spillover records for work items that moved directly into the next sprint.
    /// </summary>
    IReadOnlyList<SprintSpillover> DetectSpillover(
        IReadOnlySet<int> committedWorkItemIds,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        SprintDefinition sprint,
        string? nextSprintPath,
        DateTimeOffset sprintEnd);
}

/// <summary>
/// CDC wrapper contract for canonical sprint execution metrics formulas.
/// New CDC consumers should depend on this interface.
/// </summary>
public interface ISprintExecutionMetricsCalculator
{
    /// <summary>
    /// Calculates canonical sprint execution metrics from reconstructed story-point totals.
    /// </summary>
    Metrics.SprintExecutionMetricsResult Calculate(Metrics.SprintExecutionMetricsInput input);
}

/// <summary>
/// Builds canonical sprint story-point totals from SprintCommitment CDC inputs.
/// </summary>
public interface ISprintFactService
{
    /// <summary>
    /// Builds the canonical sprint story-point totals for the supplied sprint inputs.
    /// </summary>
    SprintFactResult BuildSprintFactResult(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, CanonicalWorkItem> canonicalWorkItemsById,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemSnapshotsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        string? nextSprintPath);
}

/// <summary>
/// Default CDC implementation of sprint commitment reconstruction.
/// </summary>
public sealed class SprintCommitmentService : ISprintCommitmentService
{
    public DateTimeOffset GetCommitmentTimestamp(DateTimeOffset sprintStart)
    {
        return SprintCommitmentLookup.GetCommitmentTimestamp(sprintStart);
    }

    public IReadOnlySet<int> BuildCommittedWorkItemIds(
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        string sprintPath,
        DateTimeOffset commitmentTimestamp)
    {
        return SprintCommitmentLookup.BuildCommittedWorkItemIds(
            workItemsById,
            iterationEventsByWorkItem,
            sprintPath,
            commitmentTimestamp);
    }

    public IReadOnlyList<SprintCommitment> BuildCommitments(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem)
    {
        if (!sprint.StartUtc.HasValue)
        {
            return [];
        }

        var commitmentTimestamp = GetCommitmentTimestamp(sprint.StartUtc.Value);

        return BuildCommittedWorkItemIds(
                workItemsById,
                iterationEventsByWorkItem,
                sprint.Path,
                commitmentTimestamp)
            .Select(workItemId => new SprintCommitment(sprint.SprintId, workItemId, commitmentTimestamp))
            .ToList();
    }
}

/// <summary>
/// Default CDC implementation of sprint scope-change detection.
/// </summary>
public sealed class SprintScopeChangeService : ISprintScopeChangeService
{
    private const string IterationPathFieldRefName = "System.IterationPath";

    public IReadOnlyList<SprintScopeAdded> DetectScopeAdded(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem)
    {
        if (!TryGetSprintWindow(sprint, out var commitmentTimestamp, out var sprintEnd))
        {
            return [];
        }

        return iterationEventsByWorkItem
            .SelectMany(pair => pair.Value)
            .Where(IsIterationPathEvent)
            .Where(activityEvent => string.Equals(activityEvent.NewValue, sprint.Path, StringComparison.OrdinalIgnoreCase))
            .Where(activityEvent =>
            {
                var eventTimestamp = FirstDoneDeliveryLookup.GetEventTimestamp(activityEvent);
                return eventTimestamp > commitmentTimestamp && eventTimestamp <= sprintEnd;
            })
            .OrderBy(activityEvent => activityEvent.TimestampUtc)
            .ThenBy(activityEvent => activityEvent.EventId)
            .ThenBy(activityEvent => activityEvent.UpdateId)
            .Select(activityEvent => new SprintScopeAdded(
                sprint.SprintId,
                activityEvent.WorkItemId,
                FirstDoneDeliveryLookup.GetEventTimestamp(activityEvent)))
            .ToList();
    }

    public IReadOnlyList<SprintScopeRemoved> DetectScopeRemoved(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem)
    {
        if (!TryGetSprintWindow(sprint, out var commitmentTimestamp, out var sprintEnd))
        {
            return [];
        }

        return iterationEventsByWorkItem
            .SelectMany(pair => pair.Value)
            .Where(IsIterationPathEvent)
            .Where(activityEvent => string.Equals(activityEvent.OldValue, sprint.Path, StringComparison.OrdinalIgnoreCase))
            .Where(activityEvent =>
            {
                var eventTimestamp = FirstDoneDeliveryLookup.GetEventTimestamp(activityEvent);
                return eventTimestamp > commitmentTimestamp && eventTimestamp <= sprintEnd;
            })
            .OrderBy(activityEvent => activityEvent.TimestampUtc)
            .ThenBy(activityEvent => activityEvent.EventId)
            .ThenBy(activityEvent => activityEvent.UpdateId)
            .Select(activityEvent => new SprintScopeRemoved(
                sprint.SprintId,
                activityEvent.WorkItemId,
                FirstDoneDeliveryLookup.GetEventTimestamp(activityEvent)))
            .ToList();
    }

    private static bool TryGetSprintWindow(
        SprintDefinition sprint,
        out DateTimeOffset commitmentTimestamp,
        out DateTimeOffset sprintEnd)
    {
        if (!sprint.StartUtc.HasValue || !sprint.EndUtc.HasValue)
        {
            commitmentTimestamp = default;
            sprintEnd = default;
            return false;
        }

        commitmentTimestamp = SprintCommitmentLookup.GetCommitmentTimestamp(sprint.StartUtc.Value);
        sprintEnd = sprint.EndUtc.Value;
        return true;
    }

    private static bool IsIterationPathEvent(FieldChangeEvent activityEvent)
    {
        return string.Equals(activityEvent.FieldRefName, IterationPathFieldRefName, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Default CDC implementation of sprint completion detection.
/// </summary>
public sealed class SprintCompletionService : ISprintCompletionService
{
    public IReadOnlyDictionary<int, DateTimeOffset> BuildFirstDoneByWorkItem(
        IEnumerable<FieldChangeEvent> activityEvents,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        return FirstDoneDeliveryLookup.Build(activityEvents, workItemsById, stateLookup);
    }

    public IReadOnlyList<SprintCompletion> DetectCompletions(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, DateTimeOffset> firstDoneByWorkItem)
    {
        if (!sprint.StartUtc.HasValue || !sprint.EndUtc.HasValue)
        {
            return [];
        }

        return firstDoneByWorkItem
            .Where(pair => pair.Value >= sprint.StartUtc.Value && pair.Value <= sprint.EndUtc.Value)
            .OrderBy(pair => pair.Value.UtcDateTime)
            .ThenBy(pair => pair.Key)
            .Select(pair => new SprintCompletion(sprint.SprintId, pair.Key, pair.Value))
            .ToList();
    }
}

/// <summary>
/// Default CDC implementation of sprint spillover detection.
/// </summary>
public sealed class SprintSpilloverService : ISprintSpilloverService
{
    private const string IterationPathFieldRefName = "System.IterationPath";

    public string? GetNextSprintPath(
        SprintDefinition sprint,
        IEnumerable<SprintDefinition> teamSprints)
    {
        return SprintSpilloverLookup.GetNextSprintPath(sprint, teamSprints);
    }

    public IReadOnlySet<int> BuildSpilloverWorkItemIds(
        IReadOnlySet<int> committedWorkItemIds,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        SprintDefinition sprint,
        string? nextSprintPath,
        DateTimeOffset sprintEnd)
    {
        return SprintSpilloverLookup.BuildSpilloverWorkItemIds(
            committedWorkItemIds,
            workItemsById,
            stateEventsByWorkItem,
            iterationEventsByWorkItem,
            stateLookup,
            sprint,
            nextSprintPath,
            sprintEnd);
    }

    public IReadOnlyList<SprintSpillover> DetectSpillover(
        IReadOnlySet<int> committedWorkItemIds,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        SprintDefinition sprint,
        string? nextSprintPath,
        DateTimeOffset sprintEnd)
    {
        var spilloverIds = BuildSpilloverWorkItemIds(
            committedWorkItemIds,
            workItemsById,
            stateEventsByWorkItem,
            iterationEventsByWorkItem,
            stateLookup,
            sprint,
            nextSprintPath,
            sprintEnd);

        var normalizedSprintPath = NormalizeIterationPath(sprint.Path);
        var normalizedNextSprintPath = NormalizeIterationPath(nextSprintPath);

        return spilloverIds
            .Select(workItemId =>
            {
                var spilloverTimestamp = iterationEventsByWorkItem
                    .GetValueOrDefault(workItemId, Array.Empty<FieldChangeEvent>())
                    .Where(IsIterationPathEvent)
                    .Where(activityEvent => FirstDoneDeliveryLookup.GetEventTimestamp(activityEvent) >= sprintEnd)
                    .OrderBy(activityEvent => activityEvent.TimestampUtc)
                    .ThenBy(activityEvent => activityEvent.EventId)
                    .ThenBy(activityEvent => activityEvent.UpdateId)
                    .First(activityEvent =>
                        string.Equals(NormalizeIterationPath(activityEvent.OldValue), normalizedSprintPath, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(NormalizeIterationPath(activityEvent.NewValue), normalizedNextSprintPath, StringComparison.OrdinalIgnoreCase));

                return new SprintSpillover(
                    sprint.SprintId,
                    workItemId,
                    FirstDoneDeliveryLookup.GetEventTimestamp(spilloverTimestamp));
            })
            .OrderBy(spillover => spillover.SpilloverAt.UtcDateTime)
            .ThenBy(spillover => spillover.WorkItemId)
            .ToList();
    }

    private static bool IsIterationPathEvent(FieldChangeEvent activityEvent)
    {
        return string.Equals(activityEvent.FieldRefName, IterationPathFieldRefName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeIterationPath(string? iterationPath)
    {
        return string.IsNullOrWhiteSpace(iterationPath)
            ? null
            : iterationPath.Trim();
    }
}

/// <summary>
/// Default CDC wrapper for canonical sprint execution metric formulas.
/// This type also implements the legacy metrics interface so existing application handlers can migrate
/// to the CDC namespace incrementally without duplicating the underlying formula implementation.
/// </summary>
public sealed class SprintExecutionMetricsCalculator : ISprintExecutionMetricsCalculator, Metrics.ISprintExecutionMetricsCalculator
{
    private readonly Metrics.SprintExecutionMetricsCalculator _innerCalculator = new();

    public Metrics.SprintExecutionMetricsResult Calculate(Metrics.SprintExecutionMetricsInput input)
    {
        return _innerCalculator.Calculate(input);
    }
}

/// <summary>
/// Default CDC implementation for canonical sprint story-point totals.
/// </summary>
public sealed class SprintFactService : ISprintFactService
{
    private readonly ISprintCommitmentService _sprintCommitmentService;
    private readonly ISprintScopeChangeService _sprintScopeChangeService;
    private readonly ISprintCompletionService _sprintCompletionService;
    private readonly ISprintSpilloverService _sprintSpilloverService;
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintFactService"/> class.
    /// </summary>
    public SprintFactService(
        ISprintCommitmentService sprintCommitmentService,
        ISprintScopeChangeService sprintScopeChangeService,
        ISprintCompletionService sprintCompletionService,
        ISprintSpilloverService sprintSpilloverService,
        ICanonicalStoryPointResolutionService storyPointResolutionService)
    {
        _sprintCommitmentService = sprintCommitmentService ?? throw new ArgumentNullException(nameof(sprintCommitmentService));
        _sprintScopeChangeService = sprintScopeChangeService ?? throw new ArgumentNullException(nameof(sprintScopeChangeService));
        _sprintCompletionService = sprintCompletionService ?? throw new ArgumentNullException(nameof(sprintCompletionService));
        _sprintSpilloverService = sprintSpilloverService ?? throw new ArgumentNullException(nameof(sprintSpilloverService));
        _storyPointResolutionService = storyPointResolutionService ?? throw new ArgumentNullException(nameof(storyPointResolutionService));
    }

    public SprintFactResult BuildSprintFactResult(
        SprintDefinition sprint,
        IReadOnlyDictionary<int, CanonicalWorkItem> canonicalWorkItemsById,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemSnapshotsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        string? nextSprintPath)
    {
        ArgumentNullException.ThrowIfNull(sprint);
        ArgumentNullException.ThrowIfNull(canonicalWorkItemsById);
        ArgumentNullException.ThrowIfNull(workItemSnapshotsById);
        ArgumentNullException.ThrowIfNull(iterationEventsByWorkItem);
        ArgumentNullException.ThrowIfNull(stateEventsByWorkItem);

        var commitmentTimestamp = sprint.StartUtc.HasValue
            ? _sprintCommitmentService.GetCommitmentTimestamp(sprint.StartUtc.Value)
            : (DateTimeOffset?)null;

        var committedWorkItemIds = commitmentTimestamp.HasValue
            ? _sprintCommitmentService.BuildCommittedWorkItemIds(
                workItemSnapshotsById,
                iterationEventsByWorkItem,
                sprint.Path,
                commitmentTimestamp.Value)
            : workItemSnapshotsById.Values
                .Where(workItem => string.Equals(workItem.CurrentIterationPath, sprint.Path, StringComparison.OrdinalIgnoreCase))
                .Select(workItem => workItem.WorkItemId)
                .ToHashSet();

        var addedWorkItemIds = sprint.StartUtc.HasValue && sprint.EndUtc.HasValue
            ? _sprintScopeChangeService
                .DetectScopeAdded(sprint, iterationEventsByWorkItem)
                .Select(entry => entry.WorkItemId)
                .ToHashSet()
            : new HashSet<int>();

        var currentSprintItemIds = workItemSnapshotsById.Values
            .Where(workItem => string.Equals(workItem.CurrentIterationPath, sprint.Path, StringComparison.OrdinalIgnoreCase))
            .Select(workItem => workItem.WorkItemId)
            .ToHashSet();

        var removedWorkItemIds = sprint.StartUtc.HasValue && sprint.EndUtc.HasValue
            ? _sprintScopeChangeService
                .DetectScopeRemoved(sprint, iterationEventsByWorkItem)
                .Select(entry => entry.WorkItemId)
                .Distinct()
                .Except(currentSprintItemIds)
                .ToHashSet()
            : new HashSet<int>();

        var firstDoneByWorkItem = sprint.EndUtc.HasValue
            ? _sprintCompletionService.BuildFirstDoneByWorkItem(
                stateEventsByWorkItem.Values.SelectMany(events => events),
                workItemSnapshotsById,
                stateLookup)
            : new Dictionary<int, DateTimeOffset>();

        var deliveredWorkItemIds = sprint.StartUtc.HasValue && sprint.EndUtc.HasValue
            ? firstDoneByWorkItem
                .Where(pair => pair.Value >= sprint.StartUtc.Value && pair.Value <= sprint.EndUtc.Value)
                .Select(pair => pair.Key)
                .ToHashSet()
            : new HashSet<int>();

        var spilloverWorkItemIds = sprint.EndUtc.HasValue
            ? _sprintSpilloverService.BuildSpilloverWorkItemIds(
                committedWorkItemIds,
                workItemSnapshotsById,
                stateEventsByWorkItem,
                iterationEventsByWorkItem,
                stateLookup,
                sprint,
                nextSprintPath,
                sprint.EndUtc.Value)
            : new HashSet<int>();

        var committedStoryPoints = SumStoryPoints(committedWorkItemIds, canonicalWorkItemsById, workItemSnapshotsById, stateLookup, excludeDerived: true);
        var addedStoryPoints = SumStoryPoints(addedWorkItemIds, canonicalWorkItemsById, workItemSnapshotsById, stateLookup, excludeDerived: false);
        var removedStoryPoints = SumStoryPoints(removedWorkItemIds, canonicalWorkItemsById, workItemSnapshotsById, stateLookup, excludeDerived: false);
        var deliveredStoryPoints = SumStoryPoints(deliveredWorkItemIds, canonicalWorkItemsById, workItemSnapshotsById, stateLookup, excludeDerived: true, forceDone: true);
        var deliveredFromAddedStoryPoints = SumStoryPoints(deliveredWorkItemIds.Intersect(addedWorkItemIds), canonicalWorkItemsById, workItemSnapshotsById, stateLookup, excludeDerived: true, forceDone: true);
        var spilloverStoryPoints = SumStoryPoints(spilloverWorkItemIds, canonicalWorkItemsById, workItemSnapshotsById, stateLookup, excludeDerived: true);
        var remainingStoryPoints = committedStoryPoints + addedStoryPoints - removedStoryPoints - deliveredStoryPoints;

        return new SprintFactResult(
            committedStoryPoints,
            addedStoryPoints,
            removedStoryPoints,
            deliveredStoryPoints,
            deliveredFromAddedStoryPoints,
            spilloverStoryPoints,
            remainingStoryPoints);
    }

    private double SumStoryPoints(
        IEnumerable<int> workItemIds,
        IReadOnlyDictionary<int, CanonicalWorkItem> canonicalWorkItemsById,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemSnapshotsById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        bool excludeDerived,
        bool forceDone = false)
    {
        return workItemIds
            .Select(workItemId => ResolveStoryPointEstimate(
                workItemId,
                canonicalWorkItemsById,
                workItemSnapshotsById,
                stateLookup,
                excludeDerived,
                forceDone))
            .Where(estimate => estimate.HasValue)
            .Select(estimate => estimate!.Value)
            .Sum();
    }

    private double? ResolveStoryPointEstimate(
        int workItemId,
        IReadOnlyDictionary<int, CanonicalWorkItem> canonicalWorkItemsById,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemSnapshotsById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        bool excludeDerived,
        bool forceDone)
    {
        if (!canonicalWorkItemsById.TryGetValue(workItemId, out var workItem))
        {
            return null;
        }

        workItemSnapshotsById.TryGetValue(workItemId, out var snapshot);
        var isDone = forceDone || StateClassificationLookup.IsDone(stateLookup, workItem.WorkItemType, snapshot?.CurrentState);
        var estimate = _storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
            workItem,
            isDone,
            BuildFeaturePbiCandidates(workItem, canonicalWorkItemsById, workItemSnapshotsById, stateLookup)));

        if (!estimate.Value.HasValue || estimate.Source == StoryPointEstimateSource.Missing)
        {
            return null;
        }

        if (excludeDerived && estimate.Source == StoryPointEstimateSource.Derived)
        {
            return null;
        }

        return estimate.Value.Value;
    }

    /// <summary>
    /// Builds sibling PBI candidates for feature-level derived estimate resolution.
    /// Only sibling PBIs under the same parent are considered, and their canonical Done state
    /// is resolved from the supplied snapshots and state lookup.
    /// </summary>
    private static IReadOnlyCollection<StoryPointResolutionCandidate> BuildFeaturePbiCandidates(
        CanonicalWorkItem workItem,
        IReadOnlyDictionary<int, CanonicalWorkItem> canonicalWorkItemsById,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemSnapshotsById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup)
    {
        if (workItem.ParentWorkItemId == null)
        {
            return [];
        }

        return canonicalWorkItemsById.Values
            .Where(candidate => candidate.ParentWorkItemId == workItem.ParentWorkItemId && candidate.WorkItemId != workItem.WorkItemId)
            .Select(candidate => new StoryPointResolutionCandidate(
                candidate,
                workItemSnapshotsById.TryGetValue(candidate.WorkItemId, out var snapshot)
                    && StateClassificationLookup.IsDone(stateLookup, candidate.WorkItemType, snapshot.CurrentState)))
            .ToList();
    }
}
