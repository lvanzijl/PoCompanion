using PoTool.Shared.Metrics;

namespace PoTool.Core.WorkItems;

/// <summary>
/// DTO representing historical state timeline for a work item.
/// Shows how a work item has progressed through different states over time.
/// </summary>
public sealed record WorkItemStateTimelineDto(
    int WorkItemId,
    string Title,
    string Type,
    IReadOnlyList<StateTransition> StateHistory,
    IReadOnlyList<TimelineBottleneck> Bottlenecks,
    TimeSpan TotalTimeInProgress,
    TimeSpan TotalCycleTime,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// A state transition in the work item's history.
/// </summary>
public sealed record StateTransition(
    string FromState,
    string ToState,
    DateTimeOffset TransitionDate,
    string ChangedBy,
    TimeSpan TimeInPreviousState
);

/// <summary>
/// Identified bottleneck in the work item's lifecycle.
/// </summary>
public sealed record TimelineBottleneck(
    string State,
    TimeSpan TimeSpent,
    BottleneckSeverity Severity,
    string Reason
);
