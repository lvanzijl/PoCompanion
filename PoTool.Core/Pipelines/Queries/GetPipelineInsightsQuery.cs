using Mediator;
using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Retrieves per-product pipeline health insights for a single sprint.
/// All data is sourced from the local cache — no TFS calls are made.
///
/// Sprint mapping rule: a run belongs to the sprint when its FinishedDateUtc falls
/// within [SprintStartDateUtc, SprintEndDateUtc).
///
/// "Previous sprint" is the sprint immediately preceding the selected sprint in
/// the same team's sprint list (ordered by StartDateUtc ascending).
/// Delta values are null (n/a) when no previous sprint data exists.
/// </summary>
/// <param name="ProductOwnerId">Profile ID of the active Product Owner.</param>
/// <param name="SprintId">Sprint to analyse (selected sprint).</param>
/// <param name="IncludePartiallySucceeded">
///   When true (default), partiallySucceeded runs are included in completed count
///   and reported as warning builds.
///   When false, partiallySucceeded runs are excluded from all calculations.
/// </param>
/// <param name="IncludeCanceled">
///   When true, canceled runs are included in the total and completed build counts.
///   When false (default), canceled runs are excluded.
/// </param>
public sealed record GetPipelineInsightsQuery(
    int ProductOwnerId,
    int SprintId,
    bool IncludePartiallySucceeded = true,
    bool IncludeCanceled = false
) : IQuery<PipelineInsightsDto>;
