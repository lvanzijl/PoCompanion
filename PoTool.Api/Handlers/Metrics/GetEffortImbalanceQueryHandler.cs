using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortImbalanceQuery.
/// Detects disproportionate effort allocations across teams and sprints.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEffortImbalanceQueryHandler
    : IQueryHandler<GetEffortImbalanceQuery, EffortImbalanceDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetEffortImbalanceQueryHandler> _logger;

    public GetEffortImbalanceQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        ILogger<GetEffortImbalanceQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<EffortImbalanceDto> Handle(
        GetEffortImbalanceQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetEffortImbalanceQuery with AreaPathFilter: {AreaPathFilter}, Threshold: {Threshold}",
            query.AreaPathFilter ?? "All",
            query.ImbalanceThreshold);

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
            return new EffortImbalanceDto(
                TeamImbalances: Array.Empty<TeamImbalance>(),
                SprintImbalances: Array.Empty<SprintImbalance>(),
                OverallRiskLevel: ImbalanceRiskLevel.Low,
                ImbalanceScore: 0,
                Recommendations: Array.Empty<RebalancingRecommendation>(),
                AnalysisTimestamp: DateTimeOffset.UtcNow
            );
        }

        // Get top area paths by work item count
        var topAreaPaths = workItemsWithEffort
            .GroupBy(wi => wi.AreaPath)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        // Get recent iterations
        var iterationPaths = workItemsWithEffort
            .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .OrderByDescending(path => path)
            .Take(query.MaxIterations)
            .ToList();

        // Analyze team imbalances
        var teamImbalances = AnalyzeTeamImbalances(
            workItemsWithEffort,
            topAreaPaths,
            query.ImbalanceThreshold);

        // Analyze sprint imbalances
        var sprintImbalances = AnalyzeSprintImbalances(
            workItemsWithEffort,
            iterationPaths,
            query.ImbalanceThreshold,
            query.DefaultCapacityPerIteration);

        // Calculate overall risk and imbalance score
        var (overallRisk, imbalanceScore) = CalculateOverallRisk(teamImbalances, sprintImbalances);

        // Generate recommendations
        var recommendations = GenerateRecommendations(teamImbalances, sprintImbalances, overallRisk);

        return new EffortImbalanceDto(
            TeamImbalances: teamImbalances,
            SprintImbalances: sprintImbalances,
            OverallRiskLevel: overallRisk,
            ImbalanceScore: imbalanceScore,
            Recommendations: recommendations,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static List<TeamImbalance> AnalyzeTeamImbalances(
        List<WorkItemDto> workItems,
        List<string> areaPaths,
        double threshold)
    {
        var effortByArea = areaPaths
            .Select(areaPath => new
            {
                AreaPath = areaPath,
                TotalEffort = workItems
                    .Where(wi => wi.AreaPath.Equals(areaPath, StringComparison.OrdinalIgnoreCase))
                    .Sum(wi => wi.Effort ?? 0)
            })
            .ToList();

        var averageEffort = effortByArea.Average(e => e.TotalEffort);

        return effortByArea
            .Select(e =>
            {
                var deviation = Math.Abs(e.TotalEffort - averageEffort) / (averageEffort > 0 ? averageEffort : 1);
                var riskLevel = DetermineImbalanceRisk(deviation, threshold);
                var description = GenerateTeamImbalanceDescription(e.TotalEffort, averageEffort, deviation);

                return new TeamImbalance(
                    AreaPath: e.AreaPath,
                    TotalEffort: e.TotalEffort,
                    AverageEffortAcrossTeams: (int)averageEffort,
                    DeviationPercentage: deviation * 100,
                    RiskLevel: riskLevel,
                    Description: description
                );
            })
            .Where(t => t.RiskLevel != ImbalanceRiskLevel.Low)
            .OrderByDescending(t => t.DeviationPercentage)
            .ToList();
    }

    private static List<SprintImbalance> AnalyzeSprintImbalances(
        List<WorkItemDto> workItems,
        List<string> iterationPaths,
        double threshold,
        int? defaultCapacity)
    {
        var effortBySprint = iterationPaths
            .Select(iterationPath => new
            {
                IterationPath = iterationPath,
                SprintName = ExtractSprintName(iterationPath),
                TotalEffort = workItems
                    .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
                    .Sum(wi => wi.Effort ?? 0)
            })
            .ToList();

        var averageEffort = effortBySprint.Average(e => e.TotalEffort);

        return effortBySprint
            .Select(e =>
            {
                var deviation = Math.Abs(e.TotalEffort - averageEffort) / (averageEffort > 0 ? averageEffort : 1);
                var riskLevel = DetermineImbalanceRisk(deviation, threshold);
                var description = GenerateSprintImbalanceDescription(
                    e.TotalEffort,
                    averageEffort,
                    deviation,
                    defaultCapacity);

                return new SprintImbalance(
                    IterationPath: e.IterationPath,
                    SprintName: e.SprintName,
                    TotalEffort: e.TotalEffort,
                    AverageEffortAcrossSprints: (int)averageEffort,
                    DeviationPercentage: deviation * 100,
                    RiskLevel: riskLevel,
                    Description: description
                );
            })
            .Where(s => s.RiskLevel != ImbalanceRiskLevel.Low)
            .OrderByDescending(s => s.DeviationPercentage)
            .ToList();
    }

    private static ImbalanceRiskLevel DetermineImbalanceRisk(double deviation, double threshold)
    {
        // Use threshold as base, with Medium at threshold, High at 1.5x, Critical at 2x
        return deviation switch
        {
            var d when d < threshold => ImbalanceRiskLevel.Low,
            var d when d < threshold * 1.5 => ImbalanceRiskLevel.Medium,
            var d when d < threshold * 2.5 => ImbalanceRiskLevel.High,
            _ => ImbalanceRiskLevel.Critical
        };
    }

    private static (ImbalanceRiskLevel, double) CalculateOverallRisk(
        List<TeamImbalance> teamImbalances,
        List<SprintImbalance> sprintImbalances)
    {
        var allDeviations = teamImbalances
            .Select(t => t.DeviationPercentage)
            .Concat(sprintImbalances.Select(s => s.DeviationPercentage))
            .ToList();

        if (!allDeviations.Any())
        {
            return (ImbalanceRiskLevel.Low, 0);
        }

        var maxDeviation = allDeviations.Max() / 100.0;
        var avgDeviation = allDeviations.Average() / 100.0;

        // Imbalance score: weighted average of max and mean deviation
        var imbalanceScore = (maxDeviation * 0.6 + avgDeviation * 0.4) * 100;

        var overallRisk = maxDeviation switch
        {
            < 0.3 => ImbalanceRiskLevel.Low,
            >= 0.3 and < 0.5 => ImbalanceRiskLevel.Medium,
            >= 0.5 and < 0.8 => ImbalanceRiskLevel.High,
            _ => ImbalanceRiskLevel.Critical
        };

        return (overallRisk, imbalanceScore);
    }

    private static List<RebalancingRecommendation> GenerateRecommendations(
        List<TeamImbalance> teamImbalances,
        List<SprintImbalance> sprintImbalances,
        ImbalanceRiskLevel overallRisk)
    {
        var recommendations = new List<RebalancingRecommendation>();

        // Recommendations for overloaded teams
        var overloadedTeams = teamImbalances
            .Where(t => t.TotalEffort > t.AverageEffortAcrossTeams)
            .OrderByDescending(t => t.DeviationPercentage)
            .Take(3);

        foreach (var team in overloadedTeams)
        {
            var effortToMove = team.TotalEffort - team.AverageEffortAcrossTeams;
            recommendations.Add(new RebalancingRecommendation(
                Type: RecommendationType.ReduceTeamLoad,
                Title: $"Reduce load for {GetShortPath(team.AreaPath)}",
                Description: $"Team has {team.DeviationPercentage:F1}% more effort than average. " +
                            $"Consider moving {effortToMove} points to underutilized teams.",
                Priority: team.RiskLevel,
                TargetAreaPath: team.AreaPath,
                SuggestedEffortChange: -effortToMove
            ));
        }

        // Recommendations for underutilized teams
        var underloadedTeams = teamImbalances
            .Where(t => t.TotalEffort < t.AverageEffortAcrossTeams)
            .OrderBy(t => t.TotalEffort)
            .Take(3);

        foreach (var team in underloadedTeams)
        {
            var effortToAdd = team.AverageEffortAcrossTeams - team.TotalEffort;
            recommendations.Add(new RebalancingRecommendation(
                Type: RecommendationType.IncreaseTeamLoad,
                Title: $"Increase load for {GetShortPath(team.AreaPath)}",
                Description: $"Team has {team.DeviationPercentage:F1}% less effort than average. " +
                            $"Consider adding {effortToAdd} points from overloaded teams.",
                Priority: ImbalanceRiskLevel.Low,
                TargetAreaPath: team.AreaPath,
                SuggestedEffortChange: effortToAdd
            ));
        }

        // Recommendations for overloaded sprints
        var overloadedSprints = sprintImbalances
            .Where(s => s.TotalEffort > s.AverageEffortAcrossSprints)
            .OrderByDescending(s => s.DeviationPercentage)
            .Take(2);

        foreach (var sprint in overloadedSprints)
        {
            var effortToMove = sprint.TotalEffort - sprint.AverageEffortAcrossSprints;
            recommendations.Add(new RebalancingRecommendation(
                Type: RecommendationType.LevelSprintLoad,
                Title: $"Level load for {sprint.SprintName}",
                Description: $"Sprint has {sprint.DeviationPercentage:F1}% more effort than average. " +
                            $"Consider deferring {effortToMove} points to future sprints.",
                Priority: sprint.RiskLevel,
                TargetIterationPath: sprint.IterationPath,
                SuggestedEffortChange: -effortToMove
            ));
        }

        // Overall recommendation if high risk
        if (overallRisk == ImbalanceRiskLevel.High || overallRisk == ImbalanceRiskLevel.Critical)
        {
            recommendations.Insert(0, new RebalancingRecommendation(
                Type: RecommendationType.RedistributeAcrossTeams,
                Title: "Critical: Rebalance effort distribution",
                Description: "Significant imbalance detected across teams and sprints. " +
                            "Review all allocations and redistribute work to achieve 75-85% utilization target.",
                Priority: overallRisk,
                TargetAreaPath: null,
                SuggestedEffortChange: null
            ));
        }

        return recommendations;
    }

    private static string GenerateTeamImbalanceDescription(
        int actualEffort,
        double averageEffort,
        double deviation)
    {
        if (actualEffort > averageEffort)
        {
            return $"Team has {actualEffort} points vs average of {averageEffort:F0} ({deviation * 100:F1}% overload)";
        }
        else
        {
            return $"Team has {actualEffort} points vs average of {averageEffort:F0} ({deviation * 100:F1}% underload)";
        }
    }

    private static string GenerateSprintImbalanceDescription(
        int actualEffort,
        double averageEffort,
        double deviation,
        int? capacity)
    {
        var baseDesc = actualEffort > averageEffort
            ? $"Sprint has {actualEffort} points vs average of {averageEffort:F0} ({deviation * 100:F1}% overload)"
            : $"Sprint has {actualEffort} points vs average of {averageEffort:F0} ({deviation * 100:F1}% underload)";

        if (capacity.HasValue && capacity.Value > 0)
        {
            var utilization = (double)actualEffort / capacity.Value * 100;
            baseDesc += $" - {utilization:F0}% capacity utilization";
        }

        return baseDesc;
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
