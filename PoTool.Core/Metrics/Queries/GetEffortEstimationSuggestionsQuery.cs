using Mediator;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get effort estimation suggestions for work items without effort estimates.
/// Provides ML/heuristic-based suggestions using historical data.
/// </summary>
public sealed record GetEffortEstimationSuggestionsQuery(
    string? IterationPath = null,
    string? AreaPath = null,
    bool OnlyInProgressItems = true
) : IQuery<IReadOnlyList<EffortEstimationSuggestionDto>>;
