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
