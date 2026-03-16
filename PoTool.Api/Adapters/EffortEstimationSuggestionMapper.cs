using PoTool.Core.Domain.EffortPlanning;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Adapters;

internal static class EffortEstimationSuggestionMapper
{
    public static EffortEstimationSuggestionDto ToDto(this EffortEstimationSuggestionResult suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        return new EffortEstimationSuggestionDto(
            suggestion.WorkItemId,
            suggestion.WorkItemTitle,
            suggestion.WorkItemType,
            suggestion.CurrentEffort,
            suggestion.SuggestedEffort,
            suggestion.Confidence,
            BuildRationale(suggestion),
            suggestion.SimilarWorkItems
                .Select(static example => new HistoricalEffortExample(
                    example.WorkItemId,
                    example.Title,
                    example.Effort,
                    example.State,
                    example.SimilarityScore))
                .ToList());
    }

    private static string BuildRationale(EffortEstimationSuggestionResult suggestion)
    {
        return suggestion.HistoricalMatchCount == 0
            ? $"No historical data available. Using configured default {suggestion.WorkItemType} estimate."
            : suggestion.HistoricalEffortMin == suggestion.HistoricalEffortMax
                ? $"{suggestion.WorkItemType} items typically have {suggestion.SuggestedEffort} points (based on {suggestion.HistoricalMatchCount} completed items)"
                : $"{suggestion.WorkItemType} items typically range from {suggestion.HistoricalEffortMin}-{suggestion.HistoricalEffortMax} points, median {suggestion.SuggestedEffort} (based on {suggestion.HistoricalMatchCount} completed items)";
    }
}
