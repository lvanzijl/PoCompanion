using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Shared.WorkItems;
using PoTool.Core.ReleasePlanning.Commands;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for RefreshValidationCacheCommand.
/// Refreshes cached validation results for all Epics on the board.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class RefreshValidationCacheCommandHandler : ICommandHandler<RefreshValidationCacheCommand, ValidationCacheResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<RefreshValidationCacheCommandHandler> _logger;

    public RefreshValidationCacheCommandHandler(
        IReleasePlanningRepository repository,
        IWorkItemRepository workItemRepository,
        IWorkItemStateClassificationService stateClassificationService,
        IProductRepository productRepository,
        IMediator mediator,
        ILogger<RefreshValidationCacheCommandHandler> logger)
    {
        _repository = repository;
        _workItemRepository = workItemRepository;
        _stateClassificationService = stateClassificationService;
        _productRepository = productRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<ValidationCacheResultDto> Handle(
        RefreshValidationCacheCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling RefreshValidationCacheCommand");

        try
        {
            // Clear existing cache
            await _repository.ClearValidationCacheAsync(cancellationToken);

            // Get all placed Epic IDs
            var placedEpicIds = await _repository.GetPlacedEpicIdsAsync(cancellationToken);

            int errorCount = 0;
            int warningCount = 0;

            // For each Epic, compute validation and cache it
            foreach (var epicId in placedEpicIds)
            {
                var indicator = await ComputeValidationIndicatorAsync(epicId, cancellationToken);
                await _repository.UpdateCachedValidationAsync(epicId, indicator, cancellationToken);

                if (indicator == ValidationIndicator.Error) errorCount++;
                else if (indicator == ValidationIndicator.Warning) warningCount++;
            }

            return new ValidationCacheResultDto
            {
                Success = true,
                ErrorCount = errorCount,
                WarningCount = warningCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing validation cache");
            return new ValidationCacheResultDto
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ValidationIndicator> ComputeValidationIndicatorAsync(
        int epicId,
        CancellationToken cancellationToken)
    {
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
                allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
        }
        var epic = allWorkItems.FirstOrDefault(w => w.TfsId == epicId);

        if (epic == null)
        {
            return ValidationIndicator.Error;
        }

        // Find all descendants of this Epic
        var descendants = GetDescendants(epicId, allWorkItems.ToList());

        bool hasError = false;
        bool hasWarning = false;

        // Check for common validation issues
        // 1. Epic has no Features
        var features = descendants.Where(d => d.Type.Equals("Feature", StringComparison.OrdinalIgnoreCase)).ToList();
        if (features.Count == 0)
        {
            hasWarning = true;
        }

        // 2. Epic is Active but has no active work
        if (epic.State.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            var activeDescendants = descendants.Where(d =>
                d.State.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                d.State.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
                d.State.Equals("Committed", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (activeDescendants.Count == 0 && descendants.Count > 0)
            {
                hasWarning = true;
            }
        }

        // 3. Epic has missing effort estimate
        if (!epic.Effort.HasValue || epic.Effort == 0)
        {
            hasWarning = true;
        }

        // 4. Epic is Done but has Active descendants (error)
        if (await _stateClassificationService.IsDoneStateAsync(epic.Type, epic.State, cancellationToken))
        {
            var openDescendants = new List<WorkItemDto>();
            foreach (var d in descendants)
            {
                var isDone = await _stateClassificationService.IsDoneStateAsync(d.Type, d.State, cancellationToken);
                if (!isDone)
                {
                    openDescendants.Add(d);
                }
            }

            if (openDescendants.Count > 0)
            {
                hasError = true;
            }
        }

        if (hasError) return ValidationIndicator.Error;
        if (hasWarning) return ValidationIndicator.Warning;
        return ValidationIndicator.None;
    }

    private static List<WorkItemDto> GetDescendants(
        int parentId,
        List<WorkItemDto> allItems)
    {
        var descendants = new List<WorkItemDto>();
        var children = allItems.Where(w => w.ParentTfsId == parentId).ToList();

        foreach (var child in children)
        {
            descendants.Add(child);
            descendants.AddRange(GetDescendants(child.TfsId, allItems));
        }

        return descendants;
    }
}
