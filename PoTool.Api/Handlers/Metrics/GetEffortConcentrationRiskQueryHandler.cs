using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortConcentrationRiskQuery.
/// Identifies concentration risks where effort is focused in single features or areas.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEffortConcentrationRiskQueryHandler
    : IQueryHandler<GetEffortConcentrationRiskQuery, EffortConcentrationRiskDto>
{
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
            "Handling GetEffortConcentrationRiskQuery with AreaPathFilter: {AreaPathFilter}, Threshold: {Threshold}",
            query.AreaPathFilter ?? "All",
            query.ConcentrationThreshold);

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            var rootIds = productsList
                .Where(p => p.BacklogRootWorkItemId > 0)
                .Select(p => p.BacklogRootWorkItemId)
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

        var totalEffort = workItemsWithEffort.Sum(wi => wi.Effort ?? 0);

        // Get recent iterations
        var iterationPaths = workItemsWithEffort
            .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .OrderByDescending(path => path)
            .Take(query.MaxIterations)
            .ToList();

        // Analyze area path concentration
        var areaPathRisks = AnalyzeAreaPathConcentration(workItemsWithEffort, totalEffort);

        // Analyze iteration concentration
        var iterationRisks = AnalyzeIterationConcentration(workItemsWithEffort, iterationPaths, totalEffort);

        // Calculate overall concentration risk
        var (overallRisk, concentrationIndex) = CalculateOverallConcentrationRisk(
            areaPathRisks,
            iterationRisks);

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

    private static List<ConcentrationRisk> AnalyzeAreaPathConcentration(
        List<WorkItemDto> workItems,
        int totalEffort)
    {
        return workItems
            .GroupBy(wi => wi.AreaPath)
            .Select(group =>
            {
                var effortAmount = group.Sum(wi => wi.Effort ?? 0);
                var percentage = totalEffort > 0 ? (double)effortAmount / totalEffort : 0;
                var riskLevel = DetermineConcentrationRisk(percentage);
                var topWorkItems = group
                    .OrderByDescending(wi => wi.Effort ?? 0)
                    .Take(5)
                    .Select(wi => $"#{wi.TfsId}: {wi.Title} ({wi.Effort} pts)")
                    .ToList();

                var description = GenerateConcentrationDescription(
                    "area path",
                    effortAmount,
                    percentage,
                    riskLevel);

                return new ConcentrationRisk(
                    Name: GetShortPath(group.Key),
                    Path: group.Key,
                    EffortAmount: effortAmount,
                    PercentageOfTotal: percentage * 100,
                    RiskLevel: riskLevel,
                    Description: description,
                    TopWorkItems: topWorkItems
                );
            })
            .Where(r => r.RiskLevel != ConcentrationRiskLevel.None)
            .OrderByDescending(r => r.PercentageOfTotal)
            .ToList();
    }

    private static List<ConcentrationRisk> AnalyzeIterationConcentration(
        List<WorkItemDto> workItems,
        List<string> iterationPaths,
        int totalEffort)
    {
        return iterationPaths
            .Select(iterationPath =>
            {
                var itemsInIteration = workItems
                    .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var effortAmount = itemsInIteration.Sum(wi => wi.Effort ?? 0);
                var percentage = totalEffort > 0 ? (double)effortAmount / totalEffort : 0;
                var riskLevel = DetermineConcentrationRisk(percentage);
                var topWorkItems = itemsInIteration
                    .OrderByDescending(wi => wi.Effort ?? 0)
                    .Take(5)
                    .Select(wi => $"#{wi.TfsId}: {wi.Title} ({wi.Effort} pts)")
                    .ToList();

                var description = GenerateConcentrationDescription(
                    "iteration",
                    effortAmount,
                    percentage,
                    riskLevel);

                return new ConcentrationRisk(
                    Name: ExtractSprintName(iterationPath),
                    Path: iterationPath,
                    EffortAmount: effortAmount,
                    PercentageOfTotal: percentage * 100,
                    RiskLevel: riskLevel,
                    Description: description,
                    TopWorkItems: topWorkItems
                );
            })
            .Where(r => r.RiskLevel != ConcentrationRiskLevel.None)
            .OrderByDescending(r => r.PercentageOfTotal)
            .ToList();
    }

    private static ConcentrationRiskLevel DetermineConcentrationRisk(double percentage)
    {
        return percentage switch
        {
            < 0.25 => ConcentrationRiskLevel.None,
            >= 0.25 and < 0.40 => ConcentrationRiskLevel.Low,
            >= 0.40 and < 0.60 => ConcentrationRiskLevel.Medium,
            >= 0.60 and < 0.80 => ConcentrationRiskLevel.High,
            _ => ConcentrationRiskLevel.Critical
        };
    }

    private static (ConcentrationRiskLevel, double) CalculateOverallConcentrationRisk(
        List<ConcentrationRisk> areaPathRisks,
        List<ConcentrationRisk> iterationRisks)
    {
        var allRisks = areaPathRisks.Concat(iterationRisks).ToList();

        if (!allRisks.Any())
        {
            return (ConcentrationRiskLevel.None, 0);
        }

        var maxConcentration = allRisks.Max(r => r.PercentageOfTotal);

        // Herfindahl-Hirschman Index (HHI) for concentration
        // HHI = Σ(market share as decimal)² × 10,000
        // Convert percentages to decimals (0-1 range) before squaring
        var hhi = allRisks.Sum(r => Math.Pow(r.PercentageOfTotal / 100.0, 2)) * 10000;

        // HHI ranges: 0-1500 (unconcentrated), 1500-2500 (moderate), 2500+ (high concentration)
        // Normalize to 0-100 scale for UI display
        var concentrationIndex = Math.Min(100, hhi / 100);

        var overallRisk = maxConcentration switch
        {
            < 25 => ConcentrationRiskLevel.None,
            >= 25 and < 40 => ConcentrationRiskLevel.Low,
            >= 40 and < 60 => ConcentrationRiskLevel.Medium,
            >= 60 and < 80 => ConcentrationRiskLevel.High,
            _ => ConcentrationRiskLevel.Critical
        };

        return (overallRisk, concentrationIndex);
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
                            $"Consider moving ~{effortToRedistribute} points to other areas to reduce concentration risk.",
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
                            $"Consider deferring ~{effortToRedistribute} points to adjacent sprints.",
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

        return $"{riskDesc}: {effortAmount} points ({percentage * 100:F1}%) in this {entityType}";
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
