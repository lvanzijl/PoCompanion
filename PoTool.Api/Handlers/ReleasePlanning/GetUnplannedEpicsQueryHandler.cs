using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Core.ReleasePlanning.Queries;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for GetUnplannedEpicsQuery.
/// Returns all Epics that are not yet placed on the Release Planning Board.
/// </summary>
public sealed class GetUnplannedEpicsQueryHandler : IQueryHandler<GetUnplannedEpicsQuery, IReadOnlyList<UnplannedEpicDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IReleasePlanningRepository _planningRepository;
    private readonly ILogger<GetUnplannedEpicsQueryHandler> _logger;

    public GetUnplannedEpicsQueryHandler(
        IWorkItemRepository workItemRepository,
        IReleasePlanningRepository planningRepository,
        ILogger<GetUnplannedEpicsQueryHandler> logger)
    {
        _workItemRepository = workItemRepository;
        _planningRepository = planningRepository;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<UnplannedEpicDto>> Handle(
        GetUnplannedEpicsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetUnplannedEpicsQuery");

        // Get all Epics from work items
        var allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
        var epics = allWorkItems
            .Where(w => w.Type.Equals("Epic", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Get Epic IDs that are already placed on the board
        var placedEpicIds = await _planningRepository.GetPlacedEpicIdsAsync(cancellationToken);
        var placedEpicIdsSet = new HashSet<int>(placedEpicIds);

        // Filter to unplanned Epics only
        var unplannedEpics = epics
            .Where(e => !placedEpicIdsSet.Contains(e.TfsId))
            .ToList();

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

            result.Add(new UnplannedEpicDto
            {
                EpicId = epic.TfsId,
                Title = epic.Title,
                ObjectiveId = objective?.TfsId ?? 0,
                ObjectiveTitle = objective?.Title ?? "No Objective",
                Effort = epic.Effort,
                State = epic.State,
                ValidationIndicator = validation,
                TfsOrder = tfsOrder++
            });
        }

        return result;
    }
}
