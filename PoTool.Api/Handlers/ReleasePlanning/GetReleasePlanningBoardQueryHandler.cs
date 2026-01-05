using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Core.ReleasePlanning.Queries;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for GetReleasePlanningBoardQuery.
/// Returns the complete Release Planning Board state with derived connectors.
/// </summary>
public sealed class GetReleasePlanningBoardQueryHandler : IQueryHandler<GetReleasePlanningBoardQuery, ReleasePlanningBoardDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ConnectorDerivationService _connectorService;
    private readonly ILogger<GetReleasePlanningBoardQueryHandler> _logger;

    public GetReleasePlanningBoardQueryHandler(
        IReleasePlanningRepository repository,
        ConnectorDerivationService connectorService,
        ILogger<GetReleasePlanningBoardQueryHandler> logger)
    {
        _repository = repository;
        _connectorService = connectorService;
        _logger = logger;
    }

    public async ValueTask<ReleasePlanningBoardDto> Handle(
        GetReleasePlanningBoardQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetReleasePlanningBoardQuery");

        // Fetch all board data
        var lanes = await _repository.GetAllLanesAsync(cancellationToken);
        var placements = await _repository.GetAllPlacementsAsync(cancellationToken);
        var milestoneLines = await _repository.GetAllMilestoneLinesAsync(cancellationToken);
        var iterationLines = await _repository.GetAllIterationLinesAsync(cancellationToken);

        // Derive connectors (not persisted)
        var connectors = _connectorService.DeriveAllConnectors(lanes, placements);

        // Calculate row statistics
        int maxRowIndex = placements.Count > 0 ? placements.Max(p => p.RowIndex) : 0;
        int totalRows = maxRowIndex + 1;

        return new ReleasePlanningBoardDto
        {
            Lanes = lanes,
            Placements = placements,
            MilestoneLines = milestoneLines,
            IterationLines = iterationLines,
            Connectors = connectors,
            MaxRowIndex = maxRowIndex,
            TotalRows = totalRows
        };
    }
}
