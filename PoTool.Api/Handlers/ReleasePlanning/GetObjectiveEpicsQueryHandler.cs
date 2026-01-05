using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Core.ReleasePlanning.Queries;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for GetObjectiveEpicsQuery.
/// Returns all Epics for a specific Objective with their planned/unplanned status.
/// </summary>
public sealed class GetObjectiveEpicsQueryHandler : IQueryHandler<GetObjectiveEpicsQuery, IReadOnlyList<ObjectiveEpicDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IReleasePlanningRepository _planningRepository;
    private readonly ILogger<GetObjectiveEpicsQueryHandler> _logger;

    public GetObjectiveEpicsQueryHandler(
        IWorkItemRepository workItemRepository,
        IReleasePlanningRepository planningRepository,
        ILogger<GetObjectiveEpicsQueryHandler> logger)
    {
        _workItemRepository = workItemRepository;
        _planningRepository = planningRepository;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<ObjectiveEpicDto>> Handle(
        GetObjectiveEpicsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetObjectiveEpicsQuery for Objective {ObjectiveId}", query.ObjectiveId);

        // Get all work items to find Epics with the specified Objective as parent
        var allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
        var epics = allWorkItems
            .Where(w => w.Type.Equals("Epic", StringComparison.OrdinalIgnoreCase) 
                        && w.ParentTfsId == query.ObjectiveId)
            .ToList();

        // Get all placements to check which Epics are planned
        var allPlacements = await _planningRepository.GetAllPlacementsAsync(cancellationToken);
        var placementsByEpicId = allPlacements.ToDictionary(p => p.EpicId, p => p);

        var result = new List<ObjectiveEpicDto>();

        foreach (var epic in epics)
        {
            var hasPlacement = placementsByEpicId.TryGetValue(epic.TfsId, out var placement);
            var validation = await _planningRepository.GetCachedValidationAsync(epic.TfsId, cancellationToken);

            result.Add(new ObjectiveEpicDto
            {
                EpicId = epic.TfsId,
                Title = epic.Title,
                IsPlanned = hasPlacement,
                Effort = epic.Effort,
                State = epic.State,
                ValidationIndicator = validation,
                RowIndex = hasPlacement ? placement!.RowIndex : null
            });
        }

        return result;
    }
}
