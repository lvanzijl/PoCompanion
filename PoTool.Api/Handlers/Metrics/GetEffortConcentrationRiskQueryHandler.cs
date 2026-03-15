using Mediator;
using EffortDiagnosticsAnalyzer = PoTool.Core.Metrics.EffortDiagnostics.EffortDiagnosticsAnalyzer;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortConcentrationRiskQuery.
/// Identifies fixed-band concentration risks where effort hours are focused in single areas or iterations.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEffortConcentrationRiskQueryHandler
    : IQueryHandler<GetEffortConcentrationRiskQuery, EffortConcentrationRiskDto>
{
    private static readonly EffortDiagnosticsAnalyzer Analyzer = new();
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetEffortConcentrationRiskQueryHandler> _logger;

    public GetEffortConcentrationRiskQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        ILogger<GetEffortConcentrationRiskQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<EffortConcentrationRiskDto> Handle(
        GetEffortConcentrationRiskQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetEffortConcentrationRiskQuery with AreaPathFilter: {AreaPathFilter}, LegacyThresholdIgnored: {Threshold}",
            query.AreaPathFilter ?? "All",
            query.ConcentrationThreshold);

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            var rootIds = productsList
                .SelectMany(p => p.BacklogRootWorkItemIds)
                .ToArray();

            if (rootIds.Length > 0)
            {
                var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootIds);
                allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);
            }
            else
            {
                allWorkItems = await _repository.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _repository.GetAllAsync(cancellationToken);
        }

        // Filter by area path if specified
        if (!string.IsNullOrWhiteSpace(query.AreaPathFilter))
        {
            allWorkItems = allWorkItems
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter to items with effort only
        var workItemsWithEffort = allWorkItems
            .Where(wi => wi.Effort.HasValue && wi.Effort.Value > 0)
            .ToList();

        if (!workItemsWithEffort.Any())
        {
            return new EffortConcentrationRiskDto(
                AreaPathRisks: Array.Empty<ConcentrationRisk>(),
                IterationRisks: Array.Empty<ConcentrationRisk>(),
                OverallRiskLevel: ConcentrationRiskLevel.None,
                ConcentrationIndex: 0,
                Recommendations: Array.Empty<RiskMitigationRecommendation>(),
                AnalysisTimestamp: DateTimeOffset.UtcNow
            );
        }

        // Get recent iterations
        var iterationPaths = workItemsWithEffort
            .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .OrderByDescending(path => path)
            .Take(query.MaxIterations)
            .ToList();

        var areaBuckets = workItemsWithEffort
            .GroupBy(wi => wi.AreaPath)
            .ToDictionary(group => group.Key, group => group.Sum(wi => wi.Effort ?? 0));
        var iterationBuckets = iterationPaths.ToDictionary(
            iterationPath => iterationPath,
            iterationPath => workItemsWithEffort
                .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
                .Sum(wi => wi.Effort ?? 0));

        var analysis = Analyzer.AnalyzeConcentration(areaBuckets, iterationBuckets);
        var allAreaPathRisks = MapAreaPathRisks(workItemsWithEffort, analysis.AreaBuckets);
        var areaPathRisks = FilterVisibleRisks(allAreaPathRisks);
        var allIterationRisks = MapIterationRisks(workItemsWithEffort, analysis.IterationBuckets);
        var iterationRisks = FilterVisibleRisks(allIterationRisks);
        var overallRisk = MapConcentrationRiskLevel(analysis.OverallRiskLevel);
        var concentrationIndex = analysis.ConcentrationIndex;

        // Generate mitigation recommendations
        var recommendations = GenerateMitigationRecommendations(
            areaPathRisks,
            iterationRisks,
            overallRisk);

        return new EffortConcentrationRiskDto(
            AreaPathRisks: areaPathRisks,
            IterationRisks: iterationRisks,
            OverallRiskLevel: overallRisk,
            ConcentrationIndex: concentrationIndex,
            Recommendations: recommendations,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static List<ConcentrationRisk> MapAreaPathRisks(
        List<WorkItemDto> workItems,
        IReadOnlyList<PoTool.Core.Metrics.EffortDiagnostics.EffortConcentrationBucket> areaBuckets)
    {
        return areaBuckets
            .Select(bucket =>
            {
                var topWorkItems = workItems
                    .Where(wi => wi.AreaPath.Equals(bucket.BucketKey, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(wi => wi.Effort ?? 0)
                    .Take(5)
                    .Select(wi => $"#{wi.TfsId}: {wi.Title} ({wi.Effort} effort hours)")
                    .ToList();

                var description = GenerateConcentrationDescription(
                    "area path",
                    (int)bucket.EffortAmount,
                    bucket.EffortShare,
                    MapConcentrationRiskLevel(bucket.RiskLevel));

                return new ConcentrationRisk(
                    Name: GetShortPath(bucket.BucketKey),
                    Path: bucket.BucketKey,
                    EffortAmount: (int)bucket.EffortAmount,
                    PercentageOfTotal: bucket.EffortShare * 100,
                    RiskLevel: MapConcentrationRiskLevel(bucket.RiskLevel),
                    Description: description,
                    TopWorkItems: topWorkItems
                );
            })
            .OrderByDescending(r => r.PercentageOfTotal)
            .ToList();
    }

    private static List<ConcentrationRisk> MapIterationRisks(
        List<WorkItemDto> workItems,
        IReadOnlyList<PoTool.Core.Metrics.EffortDiagnostics.EffortConcentrationBucket> iterationBuckets)
    {
        return iterationBuckets
            .Select(bucket =>
            {
                var itemsInIteration = workItems
                    .Where(wi => wi.IterationPath.Equals(bucket.BucketKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var topWorkItems = itemsInIteration
                    .OrderByDescending(wi => wi.Effort ?? 0)
                    .Take(5)
                    .Select(wi => $"#{wi.TfsId}: {wi.Title} ({wi.Effort} effort hours)")
                    .ToList();

                var description = GenerateConcentrationDescription(
                    "iteration",
                    (int)bucket.EffortAmount,
                    bucket.EffortShare,
                    MapConcentrationRiskLevel(bucket.RiskLevel));

                return new ConcentrationRisk(
                    Name: ExtractSprintName(bucket.BucketKey),
                    Path: bucket.BucketKey,
                    EffortAmount: (int)bucket.EffortAmount,
                    PercentageOfTotal: bucket.EffortShare * 100,
                    RiskLevel: MapConcentrationRiskLevel(bucket.RiskLevel),
                    Description: description,
                    TopWorkItems: topWorkItems
                );
            })
            .OrderByDescending(r => r.PercentageOfTotal)
            .ToList();
    }

    private static List<ConcentrationRisk> FilterVisibleRisks(List<ConcentrationRisk> risks)
    {
        return risks
            .Where(r => r.RiskLevel != ConcentrationRiskLevel.None)
            .ToList();
    }

    private static ConcentrationRiskLevel MapConcentrationRiskLevel(
        PoTool.Core.Metrics.EffortDiagnostics.ConcentrationRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            PoTool.Core.Metrics.EffortDiagnostics.ConcentrationRiskLevel.None => ConcentrationRiskLevel.None,
            PoTool.Core.Metrics.EffortDiagnostics.ConcentrationRiskLevel.Low => ConcentrationRiskLevel.Low,
            PoTool.Core.Metrics.EffortDiagnostics.ConcentrationRiskLevel.Medium => ConcentrationRiskLevel.Medium,
            PoTool.Core.Metrics.EffortDiagnostics.ConcentrationRiskLevel.High => ConcentrationRiskLevel.High,
            PoTool.Core.Metrics.EffortDiagnostics.ConcentrationRiskLevel.Critical => ConcentrationRiskLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(riskLevel), riskLevel, null)
        };
    }

    private static List<RiskMitigationRecommendation> GenerateMitigationRecommendations(
        List<ConcentrationRisk> areaPathRisks,
        List<ConcentrationRisk> iterationRisks,
        ConcentrationRiskLevel overallRisk)
    {
        var recommendations = new List<RiskMitigationRecommendation>();

        // Recommendations for high area path concentration
        var highAreaRisks = areaPathRisks
            .Where(r => r.RiskLevel >= ConcentrationRiskLevel.Medium)
            .OrderByDescending(r => r.PercentageOfTotal)
            .Take(3);

        foreach (var risk in highAreaRisks)
        {
            var effortToRedistribute = (int)(risk.EffortAmount * 0.2); // Move 20%
            recommendations.Add(new RiskMitigationRecommendation(
                Strategy: MitigationStrategy.DiversifyAcrossAreas,
                Title: $"Diversify effort from {risk.Name}",
                Description: $"Area has {risk.PercentageOfTotal:F1}% of total effort. " +
                            $"Consider moving ~{effortToRedistribute} effort hours to other areas to reduce concentration risk.",
                Priority: risk.RiskLevel,
                TargetPath: risk.Path,
                EffortToRedistribute: effortToRedistribute
            ));
        }

        // Recommendations for high iteration concentration
        var highIterationRisks = iterationRisks
            .Where(r => r.RiskLevel >= ConcentrationRiskLevel.Medium)
            .OrderByDescending(r => r.PercentageOfTotal)
            .Take(2);

        foreach (var risk in highIterationRisks)
        {
            var effortToRedistribute = (int)(risk.EffortAmount * 0.15); // Move 15%
            recommendations.Add(new RiskMitigationRecommendation(
                Strategy: MitigationStrategy.SpreadAcrossSprints,
                Title: $"Spread effort from {risk.Name}",
                Description: $"Sprint has {risk.PercentageOfTotal:F1}% of total effort. " +
                            $"Consider deferring ~{effortToRedistribute} effort hours to adjacent sprints.",
                Priority: risk.RiskLevel,
                TargetPath: risk.Path,
                EffortToRedistribute: effortToRedistribute
            ));
        }

        // Check for large individual work items that contribute to concentration
        var largeItemRisks = areaPathRisks
            .Concat(iterationRisks)
            .Where(r => r.TopWorkItems.Any())
            .ToList();

        if (largeItemRisks.Any())
        {
            recommendations.Add(new RiskMitigationRecommendation(
                Strategy: MitigationStrategy.BreakDownLargeItems,
                Title: "Break down large work items",
                Description: "Several large work items detected. Breaking them into smaller items " +
                            "allows better distribution and reduces dependency on single deliverables.",
                Priority: ConcentrationRiskLevel.Low,
                TargetPath: null,
                EffortToRedistribute: null
            ));
        }

        // Overall recommendation for critical risk
        if (overallRisk == ConcentrationRiskLevel.Critical)
        {
            recommendations.Insert(0, new RiskMitigationRecommendation(
                Strategy: MitigationStrategy.DiversifyAcrossAreas,
                Title: "Critical: Reduce concentration risk",
                Description: "Extreme effort concentration detected. Single point of failure risk is high. " +
                            "Immediately diversify work across multiple teams and sprints.",
                Priority: ConcentrationRiskLevel.Critical,
                TargetPath: null,
                EffortToRedistribute: null
            ));
        }

        // Recommendation for portfolio diversification
        if (overallRisk >= ConcentrationRiskLevel.Medium)
        {
            recommendations.Add(new RiskMitigationRecommendation(
                Strategy: MitigationStrategy.IncreaseBacklog,
                Title: "Increase backlog diversity",
                Description: "Add more work items across different areas to create a more balanced portfolio " +
                            "and reduce dependency on concentrated efforts.",
                Priority: ConcentrationRiskLevel.Low,
                TargetPath: null,
                EffortToRedistribute: null
            ));
        }

        return recommendations;
    }

    private static string GenerateConcentrationDescription(
        string entityType,
        int effortAmount,
        double percentage,
        ConcentrationRiskLevel risk)
    {
        var riskDesc = risk switch
        {
            ConcentrationRiskLevel.Critical => "CRITICAL concentration",
            ConcentrationRiskLevel.High => "High concentration",
            ConcentrationRiskLevel.Medium => "Moderate concentration",
            ConcentrationRiskLevel.Low => "Some concentration",
            _ => "Normal distribution"
        };

        return $"{riskDesc}: {effortAmount} effort hours ({percentage * 100:F1}%) in this {entityType}";
    }

    private static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }

    private static string GetShortPath(string path)
    {
        var parts = path.Split('\\', '/');
        return parts.Length > 2 ? string.Join("\\", parts.Skip(parts.Length - 2)) : path;
    }
}
