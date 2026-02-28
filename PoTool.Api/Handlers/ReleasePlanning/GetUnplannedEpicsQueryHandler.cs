using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Core.ReleasePlanning.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for GetUnplannedEpicsQuery.
/// Returns all Epics that are not yet placed on the Release Planning Board.
/// Uses product-scoped hierarchical loading when products are configured.
/// Filters out epics in Done or Removed state based on configured state mappings.
/// </summary>
public sealed class GetUnplannedEpicsQueryHandler : IQueryHandler<GetUnplannedEpicsQuery, IReadOnlyList<UnplannedEpicDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IReleasePlanningRepository _planningRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly IMediator _mediator;
    private readonly ILogger<GetUnplannedEpicsQueryHandler> _logger;

    public GetUnplannedEpicsQueryHandler(
        IWorkItemRepository workItemRepository,
        IReleasePlanningRepository planningRepository,
        IProductRepository productRepository,
        IWorkItemStateClassificationService stateClassificationService,
        IMediator mediator,
        ILogger<GetUnplannedEpicsQueryHandler> logger)
    {
        _workItemRepository = workItemRepository;
        _planningRepository = planningRepository;
        _productRepository = productRepository;
        _stateClassificationService = stateClassificationService;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<UnplannedEpicDto>> Handle(
        GetUnplannedEpicsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetUnplannedEpicsQuery");

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
                allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
        }
        var epics = allWorkItems
            .Where(w => w.Type.Equals("Epic", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Get Epic IDs that are already placed on the board
        var placedEpicIds = await _planningRepository.GetPlacedEpicIdsAsync(cancellationToken);
        var placedEpicIdsSet = new HashSet<int>(placedEpicIds);

        // Filter to unplanned Epics only, excluding Done and Removed states
        var unplannedEpics = new List<WorkItemDto>();
        int excludedCount = 0;
        foreach (var epic in epics)
        {
            if (placedEpicIdsSet.Contains(epic.TfsId))
            {
                continue;
            }

            // Check state classification
            var classification = await _stateClassificationService.GetClassificationAsync(
                epic.Type, epic.State, cancellationToken);

            // Exclude Done and Removed epics from unplanned list
            if (classification == StateClassification.Done || classification == StateClassification.Removed)
            {
                excludedCount++;
                continue;
            }

            unplannedEpics.Add(epic);
        }

        if (excludedCount > 0)
        {
            _logger.LogDebug("Excluded {Count} epics in Done or Removed state from unplanned list", excludedCount);
        }

        // Map to DTOs with parent Objective information
        var result = new List<UnplannedEpicDto>();
        int tfsOrder = 0;

        foreach (var epic in unplannedEpics)
        {
            // Find parent Objective
            var objective = epic.ParentTfsId.HasValue
                ? allWorkItems.FirstOrDefault(w => w.TfsId == epic.ParentTfsId.Value)
                : null;

            // Get cached validation
            var validation = await _planningRepository.GetCachedValidationAsync(epic.TfsId, cancellationToken);

            // Get state classification
            var stateClassification = await _stateClassificationService.GetClassificationAsync(
                epic.Type, epic.State, cancellationToken);

            result.Add(new UnplannedEpicDto
            {
                EpicId = epic.TfsId,
                Title = epic.Title,
                ObjectiveId = objective?.TfsId ?? 0,
                ObjectiveTitle = objective?.Title ?? "No Objective",
                Effort = epic.Effort,
                State = epic.State,
                ValidationIndicator = validation,
                StateClassification = stateClassification,
                TfsOrder = tfsOrder++
            });
        }

        return result;
    }
}
