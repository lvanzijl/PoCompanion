using Mediator;
using PoTool.Api.Services;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Models;
using PoTool.Core.Metrics.Services;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetMultiIterationBacklogHealthQuery.
/// Aggregates backlog health across multiple iterations and provides trend analysis.
/// Supports filtering by product (via root work item hierarchy) or area path.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetMultiIterationBacklogHealthQueryHandler
    : IQueryHandler<GetMultiIterationBacklogHealthQuery, MultiIterationBacklogHealthDto>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly ISprintRepository _sprintRepository;
    private readonly IMediator _mediator;
    private readonly IBacklogQualityAnalysisService _backlogQualityAnalysisService;
    private readonly ILogger<GetMultiIterationBacklogHealthQueryHandler> _logger;

    public GetMultiIterationBacklogHealthQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        ISprintRepository sprintRepository,
        IMediator mediator,
        IBacklogQualityAnalysisService backlogQualityAnalysisService,
        ILogger<GetMultiIterationBacklogHealthQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _sprintRepository = sprintRepository;
        _mediator = mediator;
        _backlogQualityAnalysisService = backlogQualityAnalysisService;
        _logger = logger;
    }

    public async ValueTask<MultiIterationBacklogHealthDto> Handle(
        GetMultiIterationBacklogHealthQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetMultiIterationBacklogHealthQuery with ProductIds: {ProductIds}, AreaPath: {AreaPath}, MaxIterations: {MaxIterations}",
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "None",
            query.AreaPath ?? "All",
            query.MaxIterations);

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;

        // Filter by product hierarchy if ProductIds are specified
        if (query.ProductIds != null && query.ProductIds.Length > 0)
        {
            var rootWorkItemIds = new List<int>();
            
            // Collect root work item IDs from all specified products
            foreach (var productId in query.ProductIds)
            {
                var product = await _productRepository.GetProductByIdAsync(productId, cancellationToken);
                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found, skipping", productId);
                    continue;
                }
                rootWorkItemIds.AddRange(product.BacklogRootWorkItemIds);
            }

            if (rootWorkItemIds.Count == 0)
            {
                _logger.LogWarning("No valid products found for IDs: {ProductIds}", string.Join(", ", query.ProductIds));
                // Return empty result if no valid products found
                return new MultiIterationBacklogHealthDto(
                    IterationHealth: new List<BacklogHealthDto>(),
                    Trend: new BacklogHealthTrend(
                        EffortTrend: TrendDirection.Unknown,
                        ValidationTrend: TrendDirection.Unknown,
                        BlockerTrend: TrendDirection.Unknown,
                        Summary: "No valid products found"
                    ),
                    TotalWorkItems: 0,
                    TotalIssues: 0,
                    AnalysisTimestamp: DateTimeOffset.UtcNow
                );
            }

            // Use product-scoped loading
            var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootWorkItemIds.ToArray());
            allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);

            _logger.LogDebug(
                "Filtered to {Count} work items in product hierarchies (roots: {RootIds}), deduplicated by TfsId",
                allWorkItems.Count(),
                string.Join(", ", rootWorkItemIds));
        }
        // Otherwise, use product-scoped approach or fallback
        else
        {
            var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
            var productsList = allProducts.ToList();

            if (productsList.Count > 0)
            {
                var rootIds = productsList
                    .SelectMany(p => p.BacklogRootWorkItemIds)
                    .Distinct()
                    .ToArray();

                if (rootIds.Length > 0)
                {
                    var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootIds);
                    allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);
                }
                else
                {
                    allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
                }
            }
            else
            {
                allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
            }

            // Filter by area path if specified (legacy behavior)
            if (!string.IsNullOrWhiteSpace(query.AreaPath))
            {
                allWorkItems = allWorkItems
                    .Where(wi => wi.AreaPath.StartsWith(query.AreaPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        // Get distinct iteration paths from work items
        var distinctIterationPaths = allWorkItems
            .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .ToList();

        _logger.LogDebug("Found {Count} distinct iteration paths in work items", distinctIterationPaths.Count);

        // Build SprintMetricsDto for each iteration path with dates from SprintRepository
        var sprintMetricsList = new List<SprintMetricsDto>();
        IEnumerable<SprintDto> allSprints = Enumerable.Empty<SprintDto>();
        
        try
        {
            allSprints = await _sprintRepository.GetAllSprintsAsync(cancellationToken);
            _logger.LogDebug("Retrieved {Count} sprints from repository", allSprints.Count());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve sprint data from repository, will use iteration paths without dates");
        }

        foreach (var iterationPath in distinctIterationPaths)
        {
            // Try to find matching sprint by path (case-insensitive)
            var matchingSprint = allSprints.FirstOrDefault(s => 
                s.Path.Equals(iterationPath, StringComparison.OrdinalIgnoreCase));
            
            var separators = new[] { '\\', '/' };
            var sprintName = iterationPath.Split(separators, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? iterationPath;
            
            var sprintMetrics = new SprintMetricsDto(
                IterationPath: iterationPath,
                SprintName: sprintName,
                StartDate: matchingSprint?.StartUtc,
                EndDate: matchingSprint?.EndUtc,
                CompletedStoryPoints: 0,  // Not needed for iteration selection
                PlannedStoryPoints: 0,
                CompletedWorkItemCount: 0,
                TotalWorkItemCount: 0,
                CompletedPBIs: 0,
                CompletedBugs: 0,
                CompletedTasks: 0
            );
            
            sprintMetricsList.Add(sprintMetrics);
        }

        // Use SprintWindowSelector to get appropriate iteration window with placeholders
        var selector = new SprintWindowSelector();
        var today = DateTimeOffset.UtcNow;
        
        // Determine which sprints to analyze based on MaxIterations parameter
        // For Health workspace: use date-based selection with placeholder support
        IReadOnlyList<SprintSlot> selectedSlots;
        
        if (query.MaxIterations <= 3)
        {
            // Backlog Health Analysis: current + 2 future (exactly 3 slots with placeholders)
            selectedSlots = selector.GetBacklogHealthWindow(sprintMetricsList, today);
            _logger.LogDebug("Using Backlog Health sprint window (current + 2 future, 3 slots total)");
        }
        else
        {
            // Issue Comparison: 3 past + current + 2 future (exactly 6 slots with placeholders)
            selectedSlots = selector.GetIssueComparisonWindow(sprintMetricsList, today);
            _logger.LogDebug("Using Issue Comparison sprint window (3 past + current + 2 future, 6 slots total)");
        }

        _logger.LogInformation("Selected {Count} slots for analysis ({RealCount} real sprints, {PlaceholderCount} placeholders)", 
            selectedSlots.Count,
            selectedSlots.Count(s => !s.IsPlaceholder),
            selectedSlots.Count(s => s.IsPlaceholder));

        // Calculate health for each selected slot (real sprints only; placeholders get empty health)
        var iterationHealthList = new List<BacklogHealthDto>();
        foreach (var slot in selectedSlots)
        {
            BacklogHealthDto health;
            if (slot.IsPlaceholder)
            {
                // Create placeholder health DTO with zero metrics
                health = new BacklogHealthDto(
                    IterationPath: slot.IterationPath,
                    SprintName: slot.DisplayName,
                    TotalWorkItems: 0,
                    WorkItemsWithoutEffort: 0,
                    WorkItemsInProgressWithoutEffort: 0,
                    ParentProgressIssues: 0,
                    BlockedItems: 0,
                    InProgressAtIterationEnd: 0,
                    IterationStart: slot.StartDate,
                    IterationEnd: slot.EndDate,
                    ValidationIssues: Array.Empty<ValidationIssueSummary>()
                );
            }
            else
            {
                // Calculate health for real sprint
                var calculatedHealth = await CalculateIterationHealth(slot.IterationPath, allWorkItems, cancellationToken);
                if (calculatedHealth == null)
                {
                    // Sprint has no work items, create empty health DTO
                    health = new BacklogHealthDto(
                        IterationPath: slot.IterationPath,
                        SprintName: slot.DisplayName,
                        TotalWorkItems: 0,
                        WorkItemsWithoutEffort: 0,
                        WorkItemsInProgressWithoutEffort: 0,
                        ParentProgressIssues: 0,
                        BlockedItems: 0,
                        InProgressAtIterationEnd: 0,
                        IterationStart: slot.StartDate,
                        IterationEnd: slot.EndDate,
                        ValidationIssues: Array.Empty<ValidationIssueSummary>()
                    );
                }
                else
                {
                    health = calculatedHealth;
                }
            }
            
            iterationHealthList.Add(health);
        }

        // Calculate trend
        var trend = CalculateTrend(iterationHealthList);

        // Calculate totals
        var totalWorkItems = iterationHealthList.Sum(h => h.TotalWorkItems);
        var totalIssues = iterationHealthList.Sum(h =>
            h.WorkItemsWithoutEffort +
            h.ParentProgressIssues +
            h.BlockedItems);

        return new MultiIterationBacklogHealthDto(
            IterationHealth: iterationHealthList,
            Trend: trend,
            TotalWorkItems: totalWorkItems,
            TotalIssues: totalIssues,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private async ValueTask<BacklogHealthDto?> CalculateIterationHealth(
        string iterationPath,
        IEnumerable<WorkItemDto> allWorkItems,
        CancellationToken cancellationToken)
    {
        var iterationWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!iterationWorkItems.Any())
        {
            return null;
        }

        var (startDate, endDate) = ExtractSprintDates(iterationWorkItems);
        var analysis = await _backlogQualityAnalysisService.AnalyzeAsync(iterationWorkItems, cancellationToken);

        return BacklogHealthDtoFactory.Create(
            iterationPath,
            iterationWorkItems,
            analysis,
            startDate,
            endDate);
    }

    private static BacklogHealthTrend CalculateTrend(List<BacklogHealthDto> iterations)
    {
        if (iterations.Count < 2)
        {
            return new BacklogHealthTrend(
                EffortTrend: TrendDirection.Unknown,
                ValidationTrend: TrendDirection.Unknown,
                BlockerTrend: TrendDirection.Unknown,
                Summary: "Insufficient data for trend analysis"
            );
        }

        // Compare most recent sprint to previous sprint
        var recent = iterations[0];
        var previous = iterations[1];

        // Calculate effort trend
        var effortTrend = CalculateTrendDirection(
            recent.WorkItemsWithoutEffort,
            previous.WorkItemsWithoutEffort,
            recent.TotalWorkItems,
            previous.TotalWorkItems
        );

        // Calculate validation trend
        var recentValidationIssues = recent.ValidationIssues.Sum(v => v.Count);
        var previousValidationIssues = previous.ValidationIssues.Sum(v => v.Count);
        var validationTrend = CalculateTrendDirection(
            recentValidationIssues,
            previousValidationIssues,
            recent.TotalWorkItems,
            previous.TotalWorkItems
        );

        // Calculate blocker trend
        var blockerTrend = CalculateTrendDirection(
            recent.BlockedItems,
            previous.BlockedItems,
            recent.TotalWorkItems,
            previous.TotalWorkItems
        );

        // Generate summary
        var summary = GenerateTrendSummary(effortTrend, validationTrend, blockerTrend);

        return new BacklogHealthTrend(
            EffortTrend: effortTrend,
            ValidationTrend: validationTrend,
            BlockerTrend: blockerTrend,
            Summary: summary
        );
    }

    private static TrendDirection CalculateTrendDirection(
        int recentCount,
        int previousCount,
        int recentTotal,
        int previousTotal)
    {
        if (recentTotal == 0 || previousTotal == 0)
        {
            return TrendDirection.Unknown;
        }

        var recentPercentage = (double)recentCount / recentTotal;
        var previousPercentage = (double)previousCount / previousTotal;
        var change = recentPercentage - previousPercentage;

        return change switch
        {
            < -0.05 => TrendDirection.Improving, // 5% improvement
            > 0.05 => TrendDirection.Degrading,  // 5% degradation
            _ => TrendDirection.Stable
        };
    }

    private static string GenerateTrendSummary(
        TrendDirection effortTrend,
        TrendDirection validationTrend,
        TrendDirection blockerTrend)
    {
        var trends = new[] { effortTrend, validationTrend, blockerTrend };
        var improving = trends.Count(t => t == TrendDirection.Improving);
        var degrading = trends.Count(t => t == TrendDirection.Degrading);

        if (improving > degrading)
        {
            return "Overall backlog health is improving";
        }
        else if (degrading > improving)
        {
            return "Overall backlog health is degrading - attention needed";
        }
        else
        {
            return "Backlog health is stable";
        }
    }

    private static (DateTimeOffset?, DateTimeOffset?) ExtractSprintDates(List<WorkItemDto> workItems)
    {
        return (null, null);
    }
}
