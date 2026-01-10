using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Shared.WorkItems;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for RefreshValidationCacheCommand.
/// Refreshes cached validation results for all Epics on the board.
/// </summary>
public sealed class RefreshValidationCacheCommandHandler : ICommandHandler<RefreshValidationCacheCommand, ValidationCacheResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ILogger<RefreshValidationCacheCommandHandler> _logger;

    public RefreshValidationCacheCommandHandler(
        IReleasePlanningRepository repository,
        IWorkItemRepository workItemRepository,
        ILogger<RefreshValidationCacheCommandHandler> logger)
    {
        _repository = repository;
        _workItemRepository = workItemRepository;
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
        // Get the Epic and its descendants
        var allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
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

        // 4. Epic is Closed but has Active descendants (error)
        if (epic.State.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
            epic.State.Equals("Done", StringComparison.OrdinalIgnoreCase))
        {
            var openDescendants = descendants.Where(d =>
                !d.State.Equals("Closed", StringComparison.OrdinalIgnoreCase) &&
                !d.State.Equals("Done", StringComparison.OrdinalIgnoreCase) &&
                !d.State.Equals("Removed", StringComparison.OrdinalIgnoreCase))
                .ToList();

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
