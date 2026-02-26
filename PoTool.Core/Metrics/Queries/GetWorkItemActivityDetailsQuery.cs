using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to retrieve activity details for a selected work item and its descendants.
/// </summary>
public sealed record GetWorkItemActivityDetailsQuery(
    int ProductOwnerId,
    int WorkItemId,
    DateTimeOffset? PeriodStartUtc,
    DateTimeOffset? PeriodEndUtc
) : IQuery<WorkItemActivityDetailsDto?>;
