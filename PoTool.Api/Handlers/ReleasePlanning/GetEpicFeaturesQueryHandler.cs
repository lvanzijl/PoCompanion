using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.ReleasePlanning;
using PoTool.Core.ReleasePlanning.Queries;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for GetEpicFeaturesQuery.
/// Returns all Features for a specific Epic (for the Epic Split dialog).
/// </summary>
public sealed class GetEpicFeaturesQueryHandler : IQueryHandler<GetEpicFeaturesQuery, IReadOnlyList<EpicFeatureDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ILogger<GetEpicFeaturesQueryHandler> _logger;

    public GetEpicFeaturesQueryHandler(
        IWorkItemRepository workItemRepository,
        ILogger<GetEpicFeaturesQueryHandler> logger)
    {
        _workItemRepository = workItemRepository;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<EpicFeatureDto>> Handle(
        GetEpicFeaturesQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEpicFeaturesQuery for Epic {EpicId}", query.EpicId);

        // Get all work items to find Features with the specified Epic as parent
        var allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
        var features = allWorkItems
            .Where(w => w.Type.Equals("Feature", StringComparison.OrdinalIgnoreCase) 
                        && w.ParentTfsId == query.EpicId)
            .OrderBy(w => w.Title)
            .ToList();

        var result = features.Select(f => new EpicFeatureDto
        {
            FeatureId = f.TfsId,
            Title = f.Title,
            Effort = f.Effort,
            State = f.State
        }).ToList();

        return result;
    }
}
