using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintMetricsQuery.
/// Calculates metrics for a specific sprint based on work items.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetSprintMetricsQueryHandler : IQueryHandler<GetSprintMetricsQuery, SprintMetricsDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ISprintRepository _sprintRepository;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly IMediator _mediator;
    private readonly ILogger<GetSprintMetricsQueryHandler> _logger;

    public GetSprintMetricsQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        ISprintRepository sprintRepository,
        IWorkItemStateClassificationService stateClassificationService,
        IMediator mediator,
        ILogger<GetSprintMetricsQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _sprintRepository = sprintRepository;
        _stateClassificationService = stateClassificationService;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<SprintMetricsDto?> Handle(
        GetSprintMetricsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetSprintMetricsQuery for iteration: {IterationPath}", query.IterationPath);

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

        // Filter work items for this specific iteration
        var sprintWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!sprintWorkItems.Any())
        {
            _logger.LogDebug("No work items found for iteration: {IterationPath}", query.IterationPath);
            return null;
        }

        // Calculate metrics
        var completedItems = new List<WorkItemDto>();
        foreach (var wi in sprintWorkItems)
        {
            if (await _stateClassificationService.IsDoneStateAsync(wi.Type, wi.State, cancellationToken))
            {
                completedItems.Add(wi);
            }
        }

        var completedStoryPoints = completedItems
            .Where(wi => wi.Effort.HasValue)
            .Sum(wi => wi.Effort!.Value);

        var plannedStoryPoints = sprintWorkItems
            .Where(wi => wi.Effort.HasValue)
            .Sum(wi => wi.Effort!.Value);

        var completedPBIs = completedItems.Count(wi =>
            wi.Type.Equals("Product Backlog Item", StringComparison.OrdinalIgnoreCase) ||
            wi.Type.Equals("PBI", StringComparison.OrdinalIgnoreCase) ||
            wi.Type.Equals("User Story", StringComparison.OrdinalIgnoreCase));

        var completedBugs = completedItems.Count(wi =>
            wi.Type.Equals("Bug", StringComparison.OrdinalIgnoreCase));

        var completedTasks = completedItems.Count(wi =>
            wi.Type.Equals("Task", StringComparison.OrdinalIgnoreCase));

        // Extract sprint name from iteration path (last segment)
        // Handle both backslash and forward slash separators
        var separators = new[] { '\\', '/' };
        var sprintName = query.IterationPath.Split(separators, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? query.IterationPath;

        // Try to find sprint dates from SprintRepository
        DateTimeOffset? startDate = null;
        DateTimeOffset? endDate = null;
        
        try
        {
            var allSprints = await _sprintRepository.GetAllSprintsAsync(cancellationToken);
            var matchingSprint = allSprints.FirstOrDefault(s => 
                s.Path.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase));
            
            if (matchingSprint != null)
            {
                startDate = matchingSprint.StartUtc;
                endDate = matchingSprint.EndUtc;
                _logger.LogDebug("Found sprint dates for {IterationPath}: Start={Start}, End={End}", 
                    query.IterationPath, startDate, endDate);
            }
            else
            {
                _logger.LogDebug("No sprint data found for iteration path: {IterationPath}", query.IterationPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve sprint dates for {IterationPath}", query.IterationPath);
        }

        var metrics = new SprintMetricsDto(
            IterationPath: query.IterationPath,
            SprintName: sprintName,
            StartDate: startDate,
            EndDate: endDate,
            CompletedStoryPoints: completedStoryPoints,
            PlannedStoryPoints: plannedStoryPoints,
            CompletedWorkItemCount: completedItems.Count,
            TotalWorkItemCount: sprintWorkItems.Count,
            CompletedPBIs: completedPBIs,
            CompletedBugs: completedBugs,
            CompletedTasks: completedTasks
        );

        _logger.LogInformation(
            "Sprint metrics calculated for {IterationPath}: {CompletedPoints} of {PlannedPoints} story points completed",
            query.IterationPath, completedStoryPoints, plannedStoryPoints);

        return metrics;
    }
}
