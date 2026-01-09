namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing effort estimation suggestions for a work item based on historical data.
/// </summary>
public sealed record EffortEstimationSuggestionDto(
    int WorkItemId,
    string WorkItemTitle,
    string WorkItemType,
    int? CurrentEffort,
    int SuggestedEffort,
    double Confidence,
    string Rationale,
    IReadOnlyList<HistoricalEffortExample> SimilarWorkItems
);

/// <summary>
/// Historical work item example used for effort estimation.
/// </summary>
public sealed record HistoricalEffortExample(
    int WorkItemId,
    string Title,
    int Effort,
    string State,
    double SimilarityScore
);
