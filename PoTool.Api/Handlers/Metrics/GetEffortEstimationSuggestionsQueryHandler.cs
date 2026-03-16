using Mediator;
using PoTool.Api.Adapters;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortEstimationSuggestionsQuery.
/// Provides intelligent effort estimation suggestions based on historical data and ML/heuristic analysis.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEffortEstimationSuggestionsQueryHandler
    : IQueryHandler<GetEffortEstimationSuggestionsQuery, IReadOnlyList<EffortEstimationSuggestionDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly IEffortEstimationSuggestionService _effortEstimationSuggestionService;
    private readonly ILogger<GetEffortEstimationSuggestionsQueryHandler> _logger;

    public GetEffortEstimationSuggestionsQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IWorkItemStateClassificationService stateClassificationService,
        IEffortEstimationSuggestionService effortEstimationSuggestionService,
        ILogger<GetEffortEstimationSuggestionsQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _stateClassificationService = stateClassificationService;
        _effortEstimationSuggestionService = effortEstimationSuggestionService;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<EffortEstimationSuggestionDto>> Handle(
        GetEffortEstimationSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEffortEstimationSuggestionsQuery for iteration={IterationPath}, area={AreaPath}, onlyInProgress={OnlyInProgress}",
            query.IterationPath, query.AreaPath, query.OnlyInProgressItems);

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
        var completedWorkItemsWithEffort = new List<WorkItemDto>();
        foreach (var wi in workItemsList.Where(wi => wi.Effort.HasValue && wi.Effort.Value > 0))
        {
            if (await _stateClassificationService.IsDoneStateAsync(wi.Type, wi.State, cancellationToken))
            {
                completedWorkItemsWithEffort.Add(wi);
            }
        }

        _logger.LogDebug("Found {Count} completed work items with effort for historical analysis", completedWorkItemsWithEffort.Count);

        // Generate suggestions for each work item without effort
        var suggestions = new List<EffortEstimationSuggestionDto>();

        // Get effort estimation settings
        var settings = await _mediator.Send(new GetEffortEstimationSettingsQuery(), cancellationToken);
        var historicalInputs = completedWorkItemsWithEffort
            .Select(static wi => wi.ToEffortPlanningWorkItem())
            .ToList();

        foreach (var workItem in itemsWithoutEffort)
        {
            var suggestion = _effortEstimationSuggestionService.GenerateSuggestion(
                workItem.ToEffortPlanningWorkItem(),
                historicalInputs,
                settings);
            suggestions.Add(new EffortEstimationSuggestionDto(
                suggestion.WorkItemId,
                suggestion.WorkItemTitle,
                suggestion.WorkItemType,
                suggestion.CurrentEffort,
                suggestion.SuggestedEffort,
                suggestion.Confidence,
                suggestion.Rationale,
                suggestion.SimilarWorkItems
                    .Select(static example => new HistoricalEffortExample(
                        example.WorkItemId,
                        example.Title,
                        example.Effort,
                        example.State,
                        example.SimilarityScore))
                    .ToList()));
        }

        _logger.LogInformation("Generated {Count} effort estimation suggestions", suggestions.Count);

        return suggestions;
    }
}
