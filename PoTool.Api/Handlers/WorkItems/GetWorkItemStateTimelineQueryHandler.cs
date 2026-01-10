using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetWorkItemStateTimelineQuery.
/// Analyzes work item revisions to build a state transition timeline and identify bottlenecks.
/// </summary>
public sealed class GetWorkItemStateTimelineQueryHandler
    : IQueryHandler<GetWorkItemStateTimelineQuery, WorkItemStateTimelineDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetWorkItemStateTimelineQueryHandler> _logger;

    public GetWorkItemStateTimelineQueryHandler(
        IWorkItemRepository repository,
        IMediator mediator,
        ILogger<GetWorkItemStateTimelineQueryHandler> logger)
    {
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<WorkItemStateTimelineDto?> Handle(
        GetWorkItemStateTimelineQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetWorkItemStateTimelineQuery for work item: {WorkItemId}", query.WorkItemId);

        var workItem = await _repository.GetByTfsIdAsync(query.WorkItemId, cancellationToken);
        if (workItem == null)
        {
            _logger.LogDebug("Work item not found: {WorkItemId}", query.WorkItemId);
            return null;
        }

        // Get revisions for this work item
        var revisions = await _mediator.Send(new GetWorkItemRevisionsQuery(query.WorkItemId), cancellationToken);
        var revisionsList = revisions.ToList();

        if (!revisionsList.Any())
        {
            _logger.LogDebug("No revisions found for work item: {WorkItemId}", query.WorkItemId);
            // Return basic timeline with current state only
            return CreateBasicTimeline(workItem);
        }

        // Build state history from revisions
        var stateHistory = BuildStateHistory(revisionsList);

        // Identify bottlenecks
        var bottlenecks = IdentifyBottlenecks(stateHistory);

        // Calculate metrics
        var totalTimeInProgress = CalculateTotalTimeInState(stateHistory, "In Progress");
        var totalCycleTime = CalculateTotalCycleTime(stateHistory);

        return new WorkItemStateTimelineDto(
            WorkItemId: workItem.TfsId,
            Title: workItem.Title,
            Type: workItem.Type,
            StateHistory: stateHistory,
            Bottlenecks: bottlenecks,
            TotalTimeInProgress: totalTimeInProgress,
            TotalCycleTime: totalCycleTime,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static WorkItemStateTimelineDto CreateBasicTimeline(WorkItemDto workItem)
    {
        return new WorkItemStateTimelineDto(
            WorkItemId: workItem.TfsId,
            Title: workItem.Title,
            Type: workItem.Type,
            StateHistory: new List<StateTransition>(),
            Bottlenecks: new List<TimelineBottleneck>(),
            TotalTimeInProgress: TimeSpan.Zero,
            TotalCycleTime: TimeSpan.Zero,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static List<StateTransition> BuildStateHistory(List<WorkItemRevisionDto> revisions)
    {
        var history = new List<StateTransition>();
        var orderedRevisions = revisions.OrderBy(r => r.ChangedDate).ToList();

        for (int i = 1; i < orderedRevisions.Count; i++)
        {
            var previous = orderedRevisions[i - 1];
            var current = orderedRevisions[i];

            // Check if there's a state change in the field changes
            var previousState = ExtractStateFromRevision(previous) ?? "Unknown";
            var currentState = ExtractStateFromRevision(current) ?? "Unknown";

            if (previousState != currentState)
            {
                var timeInPreviousState = current.ChangedDate - previous.ChangedDate;

                history.Add(new StateTransition(
                    FromState: previousState,
                    ToState: currentState,
                    TransitionDate: current.ChangedDate,
                    ChangedBy: current.ChangedBy,
                    TimeInPreviousState: timeInPreviousState
                ));
            }
        }

        return history;
    }

    private static List<TimelineBottleneck> IdentifyBottlenecks(List<StateTransition> stateHistory)
    {
        var bottlenecks = new List<TimelineBottleneck>();

        // Group by previous state to find time spent in each state
        var timeByState = stateHistory
            .GroupBy(t => t.FromState)
            .Select(g => new
            {
                State = g.Key,
                TotalTime = TimeSpan.FromTicks(g.Sum(t => t.TimeInPreviousState.Ticks)),
                TransitionCount = g.Count()
            })
            .Where(s => s.TotalTime.TotalDays > 1) // Only consider states where we spent more than 1 day
            .OrderByDescending(s => s.TotalTime)
            .ToList();

        foreach (var state in timeByState)
        {
            var severity = state.TotalTime.TotalDays switch
            {
                > 14 => BottleneckSeverity.Critical,
                > 7 => BottleneckSeverity.High,
                > 3 => BottleneckSeverity.Medium,
                _ => BottleneckSeverity.Low
            };

            var reason = state.TotalTime.TotalDays > 7
                ? $"Spent {state.TotalTime.TotalDays:F1} days in this state"
                : $"Normal processing time";

            if (severity >= BottleneckSeverity.Medium)
            {
                bottlenecks.Add(new TimelineBottleneck(
                    State: state.State,
                    TimeSpent: state.TotalTime,
                    Severity: severity,
                    Reason: reason
                ));
            }
        }

        return bottlenecks;
    }

    private static TimeSpan CalculateTotalTimeInState(List<StateTransition> stateHistory, string stateName)
    {
        var totalTicks = stateHistory
            .Where(t => t.FromState.Equals(stateName, StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.TimeInPreviousState.Ticks);

        return TimeSpan.FromTicks(totalTicks);
    }

    private static TimeSpan CalculateTotalCycleTime(List<StateTransition> stateHistory)
    {
        if (!stateHistory.Any())
        {
            return TimeSpan.Zero;
        }

        var totalTicks = stateHistory.Sum(t => t.TimeInPreviousState.Ticks);
        return TimeSpan.FromTicks(totalTicks);
    }

    private static string? ExtractStateFromRevision(WorkItemRevisionDto revision)
    {
        // Check if the state field was changed in this revision
        if (revision.FieldChanges.TryGetValue("System.State", out var stateChange))
        {
            return stateChange.NewValue;
        }

        return null;
    }
}
