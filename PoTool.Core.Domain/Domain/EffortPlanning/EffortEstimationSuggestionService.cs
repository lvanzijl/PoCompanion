using PoTool.Core.Domain.Statistics;
using PoTool.Shared.Settings;

namespace PoTool.Core.Domain.EffortPlanning;

/// <summary>
/// Produces canonical effort-estimation suggestions from historical completed work.
/// </summary>
public interface IEffortEstimationSuggestionService
{
    /// <summary>
    /// Generates a suggestion for one work item without effort using historical completed work.
    /// </summary>
    EffortEstimationSuggestionResult GenerateSuggestion(
        EffortPlanningWorkItem workItem,
        IReadOnlyList<EffortPlanningWorkItem> historicalData,
        EffortEstimationSettingsDto settings);
}

/// <summary>
/// Implements canonical effort-estimation suggestion formulas inside the EffortPlanning CDC slice.
/// </summary>
public sealed class EffortEstimationSuggestionService : IEffortEstimationSuggestionService
{
    private const int MinSampleSizeForMaxConfidence = 10;
    private const double MaxVarianceForHighConfidence = 4d;
    private const double MinConfidenceWithNoData = 0.3d;
    private const double VarianceScalingFactor = 100d;

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "from", "as", "is", "was", "are", "be", "been"
    ];

    /// <inheritdoc />
    public EffortEstimationSuggestionResult GenerateSuggestion(
        EffortPlanningWorkItem workItem,
        IReadOnlyList<EffortPlanningWorkItem> historicalData,
        EffortEstimationSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(historicalData);
        ArgumentNullException.ThrowIfNull(settings);

        var similarByType = historicalData
            .Where(historical => string.Equals(historical.WorkItemType, workItem.WorkItemType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (similarByType.Count == 0)
        {
            var defaultEffort = settings.GetDefaultEffortForType(workItem.WorkItemType);
            return new EffortEstimationSuggestionResult(
                workItem.WorkItemId,
                workItem.Title,
                workItem.WorkItemType,
                workItem.Effort,
                defaultEffort,
                MinConfidenceWithNoData,
                0,
                defaultEffort,
                defaultEffort,
                Array.Empty<EffortHistoricalExampleResult>());
        }

        var scoredWorkItems = similarByType
            .Select(historical => new
            {
                WorkItem = historical,
                SimilarityScore = CalculateSimilarity(workItem, historical)
            })
            .OrderByDescending(static item => item.SimilarityScore)
            .Take(5)
            .ToList();

        var efforts = scoredWorkItems
            .Select(static item => item.WorkItem.Effort!.Value)
            .ToList();
        var suggestedEffort = CalculateMedian(efforts);
        var variance = StatisticsMath.Variance(efforts.Select(static value => (double)value));

        return new EffortEstimationSuggestionResult(
            workItem.WorkItemId,
            workItem.Title,
            workItem.WorkItemType,
            workItem.Effort,
            suggestedEffort,
            CalculateConfidence(scoredWorkItems.Count, variance),
            scoredWorkItems.Count,
            efforts.Min(),
            efforts.Max(),
            scoredWorkItems
                .Select(item => new EffortHistoricalExampleResult(
                    item.WorkItem.WorkItemId,
                    item.WorkItem.Title,
                    item.WorkItem.Effort!.Value,
                    item.WorkItem.State,
                    item.SimilarityScore))
                .ToList());
    }

    private static double CalculateSimilarity(EffortPlanningWorkItem target, EffortPlanningWorkItem historical)
    {
        double score = 0d;

        if (string.Equals(target.WorkItemType, historical.WorkItemType, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.4d;
        }

        if (string.Equals(target.AreaPath, historical.AreaPath, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.3d;
        }
        else if (historical.AreaPath.StartsWith(target.AreaPath, StringComparison.OrdinalIgnoreCase)
            || target.AreaPath.StartsWith(historical.AreaPath, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.15d;
        }

        score += CalculateTitleSimilarity(target.Title, historical.Title) * 0.3d;
        return Math.Min(1d, score);
    }

    private static double CalculateTitleSimilarity(string title1, string title2)
    {
        var words1 = TokenizeTitle(title1);
        var words2 = TokenizeTitle(title2);

        if (words1.Count == 0 || words2.Count == 0)
        {
            return 0d;
        }

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();
        var jaccardSimilarity = union > 0 ? (double)intersection / union : 0d;
        var characterSimilarity = CalculateCharacterOverlap(string.Concat(words1), string.Concat(words2));

        return (jaccardSimilarity * 0.7d) + (characterSimilarity * 0.3d);
    }

    private static List<string> TokenizeTitle(string title)
    {
        return title.ToLowerInvariant()
            .Split([' ', '-', '_', ',', '.', ':', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 2 && !StopWords.Contains(word))
            .ToList();
    }

    private static double CalculateCharacterOverlap(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
        {
            return 0d;
        }

        var chars1 = str1.ToHashSet();
        var chars2 = str2.ToHashSet();
        var union = chars1.Union(chars2).Count();
        return union > 0 ? (double)chars1.Intersect(chars2).Count() / union : 0d;
    }

    private static int CalculateMedian(IReadOnlyList<int> values)
    {
        return (int)StatisticsMath.Median(values.Select(static value => (double)value));
    }

    private static double CalculateConfidence(int sampleSize, double variance)
    {
        var sampleConfidence = Math.Min(1d, sampleSize / (double)MinSampleSizeForMaxConfidence);
        var varianceConfidence = variance < MaxVarianceForHighConfidence
            ? 1d
            : Math.Max(MinConfidenceWithNoData, 1d - (variance / VarianceScalingFactor));

        return (sampleConfidence + varianceConfidence) / 2d;
    }
}
