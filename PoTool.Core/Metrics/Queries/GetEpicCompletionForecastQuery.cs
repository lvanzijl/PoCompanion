using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get completion forecast for an Epic or Feature.
/// </summary>
public sealed record GetEpicCompletionForecastQuery(
    int EpicId,
    int? MaxSprintsForVelocity = 5
) : IQuery<EpicCompletionForecastDto?>;
