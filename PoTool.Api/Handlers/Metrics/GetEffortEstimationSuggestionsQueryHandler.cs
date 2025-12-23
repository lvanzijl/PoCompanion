using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortEstimationSuggestionsQuery.
/// Provides intelligent effort estimation suggestions based on historical data and ML/heuristic analysis.
/// </summary>
public sealed class GetEffortEstimationSuggestionsQueryHandler 
    : IQueryHandler<GetEffortEstimationSuggestionsQuery, IReadOnlyList<EffortEstimationSuggestionDto>>
{
    // Confidence calculation constants
    private const int MinSampleSizeForMaxConfidence = 10;
    private const double MaxVarianceForHighConfidence = 4.0;
    private const double MinConfidenceWithNoData = 0.3;
    private const double VarianceScalingFactor = 100.0;

    private readonly IWorkItemRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetEffortEstimationSuggestionsQueryHandler> _logger;

    public GetEffortEstimationSuggestionsQueryHandler(
        IWorkItemRepository repository,
        IMediator mediator,
        ILogger<GetEffortEstimationSuggestionsQueryHandler> logger)
    {
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<EffortEstimationSuggestionDto>> Handle(
        GetEffortEstimationSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEffortEstimationSuggestionsQuery for iteration={IterationPath}, area={AreaPath}, onlyInProgress={OnlyInProgress}",
            query.IterationPath, query.AreaPath, query.OnlyInProgressItems);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        var workItemsList = allWorkItems.ToList();

        // Filter work items without effort
        var itemsWithoutEffort = workItemsList
            .Where(wi => !wi.Effort.HasValue || wi.Effort.Value == 0)
            .ToList();

        // Apply filters
        if (!string.IsNullOrEmpty(query.IterationPath))
        {
            itemsWithoutEffort = itemsWithoutEffort
                .Where(wi => wi.IterationPath.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrEmpty(query.AreaPath))
        {
            itemsWithoutEffort = itemsWithoutEffort
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (query.OnlyInProgressItems)
        {
            itemsWithoutEffort = itemsWithoutEffort
                .Where(wi => wi.State.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
                             wi.State.Equals("Active", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _logger.LogDebug("Found {Count} work items without effort matching criteria", itemsWithoutEffort.Count);

        // Get historical completed work items with effort for analysis
        var completedWorkItemsWithEffort = workItemsList
            .Where(wi => wi.Effort.HasValue && wi.Effort.Value > 0)
            .Where(wi => wi.State.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                         wi.State.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                         wi.State.Equals("Resolved", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _logger.LogDebug("Found {Count} completed work items with effort for historical analysis", completedWorkItemsWithEffort.Count);

        // Generate suggestions for each work item without effort
        var suggestions = new List<EffortEstimationSuggestionDto>();

        // Get effort estimation settings
        var settings = await _mediator.Send(new GetEffortEstimationSettingsQuery(), cancellationToken);

        foreach (var workItem in itemsWithoutEffort)
        {
            var suggestion = GenerateSuggestion(workItem, completedWorkItemsWithEffort, settings);
            suggestions.Add(suggestion);
        }

        _logger.LogInformation("Generated {Count} effort estimation suggestions", suggestions.Count);

        return suggestions;
    }

    private EffortEstimationSuggestionDto GenerateSuggestion(
        WorkItemDto workItem,
        List<WorkItemDto> historicalData,
        Core.Settings.EffortEstimationSettingsDto settings)
    {
        // Find similar work items by type
        var similarByType = historicalData
            .Where(wi => wi.Type.Equals(workItem.Type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (similarByType.Count == 0)
        {
            // No historical data for this type, use default estimation from settings
            var defaultEffort = settings.GetDefaultEffortForType(workItem.Type);
            return new EffortEstimationSuggestionDto(
                WorkItemId: workItem.TfsId,
                WorkItemTitle: workItem.Title,
                WorkItemType: workItem.Type,
                CurrentEffort: workItem.Effort,
                SuggestedEffort: defaultEffort,
                Confidence: MinConfidenceWithNoData,
                Rationale: $"No historical data available. Using configured default {workItem.Type} estimate.",
                SimilarWorkItems: new List<HistoricalEffortExample>()
            );
        }

        // Calculate similarity scores and find most similar work items
        var scoredWorkItems = similarByType
            .Select(wi => new
            {
                WorkItem = wi,
                SimilarityScore = CalculateSimilarity(workItem, wi)
            })
            .OrderByDescending(x => x.SimilarityScore)
            .Take(5)
            .ToList();

        // Calculate suggested effort based on similar items
        var efforts = scoredWorkItems.Select(x => x.WorkItem.Effort!.Value).ToList();
        var suggestedEffort = CalculateMedian(efforts);
        var minEffort = efforts.Min();
        var maxEffort = efforts.Max();

        // Calculate confidence based on variance and sample size
        var variance = CalculateVariance(efforts);
        var confidence = CalculateConfidence(scoredWorkItems.Count, variance);

        // Build rationale
        var rationale = BuildRationale(workItem.Type, suggestedEffort, minEffort, maxEffort, scoredWorkItems.Count);

        // Build similar work items list
        var similarWorkItems = scoredWorkItems
            .Select(x => new HistoricalEffortExample(
                WorkItemId: x.WorkItem.TfsId,
                Title: x.WorkItem.Title,
                Effort: x.WorkItem.Effort!.Value,
                State: x.WorkItem.State,
                SimilarityScore: x.SimilarityScore
            ))
            .ToList();

        return new EffortEstimationSuggestionDto(
            WorkItemId: workItem.TfsId,
            WorkItemTitle: workItem.Title,
            WorkItemType: workItem.Type,
            CurrentEffort: workItem.Effort,
            SuggestedEffort: suggestedEffort,
            Confidence: confidence,
            Rationale: rationale,
            SimilarWorkItems: similarWorkItems
        );
    }

    private double CalculateSimilarity(WorkItemDto target, WorkItemDto historical)
    {
        double score = 0.0;

        // Same type: +40%
        if (target.Type.Equals(historical.Type, StringComparison.OrdinalIgnoreCase))
            score += 0.4;

        // Same area path: +30%
        if (target.AreaPath.Equals(historical.AreaPath, StringComparison.OrdinalIgnoreCase))
            score += 0.3;
        else if (historical.AreaPath.StartsWith(target.AreaPath, StringComparison.OrdinalIgnoreCase) ||
                 target.AreaPath.StartsWith(historical.AreaPath, StringComparison.OrdinalIgnoreCase))
            score += 0.15; // Partial area path match

        // Title similarity (simple keyword matching): +30%
        var titleSimilarity = CalculateTitleSimilarity(target.Title, historical.Title);
        score += titleSimilarity * 0.3;

        return Math.Min(1.0, score);
    }

    private double CalculateTitleSimilarity(string title1, string title2)
    {
        // Enhanced similarity algorithm with stop word filtering and n-gram analysis
        var stopWords = new HashSet<string> { "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "from", "as", "is", "was", "are", "be", "been" };
        
        // Tokenize and clean
        var words1 = title1.ToLowerInvariant()
            .Split(new[] { ' ', '-', '_', ',', '.', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToList();
            
        var words2 = title2.ToLowerInvariant()
            .Split(new[] { ' ', '-', '_', ',', '.', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToList();

        if (words1.Count == 0 || words2.Count == 0)
            return 0.0;

        // Calculate Jaccard similarity (intersection / union)
        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();
        var jaccardSimilarity = union > 0 ? (double)intersection / union : 0.0;

        // Calculate character-level similarity for partial word matches (edit distance approximation)
        var characterSimilarity = CalculateCharacterOverlap(string.Join("", words1), string.Join("", words2));

        // Weighted combination: 70% word-level, 30% character-level
        return (jaccardSimilarity * 0.7) + (characterSimilarity * 0.3);
    }

    private double CalculateCharacterOverlap(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        var chars1 = str1.ToHashSet();
        var chars2 = str2.ToHashSet();
        
        var intersection = chars1.Intersect(chars2).Count();
        var union = chars1.Union(chars2).Count();
        
        return union > 0 ? (double)intersection / union : 0.0;
    }

    private int CalculateMedian(List<int> values)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2;
        else
            return sorted[mid];
    }

    private double CalculateVariance(List<int> values)
    {
        if (values.Count <= 1)
            return 0.0;

        var mean = values.Average();
        var sumOfSquaredDifferences = values.Sum(val => Math.Pow(val - mean, 2));
        return sumOfSquaredDifferences / values.Count;
    }

    private double CalculateConfidence(int sampleSize, double variance)
    {
        // Base confidence on sample size and variance
        var sampleConfidence = Math.Min(1.0, sampleSize / (double)MinSampleSizeForMaxConfidence);
        var varianceConfidence = variance < MaxVarianceForHighConfidence 
            ? 1.0 
            : Math.Max(MinConfidenceWithNoData, 1.0 - (variance / VarianceScalingFactor));

        return (sampleConfidence + varianceConfidence) / 2.0;
    }

    private string BuildRationale(string type, int suggested, int min, int max, int sampleCount)
    {
        if (min == max)
        {
            return $"{type} items typically have {suggested} points (based on {sampleCount} completed items)";
        }
        else
        {
            return $"{type} items typically range from {min}-{max} points, median {suggested} (based on {sampleCount} completed items)";
        }
    }
}
