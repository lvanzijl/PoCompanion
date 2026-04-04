using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Client.Services;

/// <summary>
/// Service for Release Planning Board operations.
/// </summary>
public class ReleasePlanningService
{
    private readonly IReleasePlanningClient _releasePlanningClient;
    private readonly ILogger<ReleasePlanningService> _logger;

    public ReleasePlanningService(
        IReleasePlanningClient releasePlanningClient,
        ILogger<ReleasePlanningService> logger)
    {
        _releasePlanningClient = releasePlanningClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the complete Release Planning Board state.
    /// </summary>
    public async Task<ReleasePlanningBoardDto?> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<ReleasePlanningBoardDto>(
                await _releasePlanningClient.GetBoardAsync(cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error retrieving Release Planning Board");
            return null;
        }
    }

    /// <summary>
    /// Gets all unplanned Epics.
    /// </summary>
    public async Task<IReadOnlyList<UnplannedEpicDto>?> GetUnplannedEpicsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault(
                await _releasePlanningClient.GetUnplannedEpicsAsync(cancellationToken),
                static data => data.ToReadOnlyList(),
                Array.Empty<UnplannedEpicDto>());
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error retrieving unplanned Epics");
            return null;
        }
    }

    /// <summary>
    /// Gets all Epics for a specific Objective.
    /// </summary>
    public async Task<IReadOnlyList<ObjectiveEpicDto>?> GetObjectiveEpicsAsync(
        int objectiveId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault(
                await _releasePlanningClient.GetObjectiveEpicsAsync(objectiveId, cancellationToken),
                static data => data.ToReadOnlyList(),
                Array.Empty<ObjectiveEpicDto>());
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error retrieving Epics for Objective {ObjectiveId}", objectiveId);
            return null;
        }
    }

    #region Lane Operations

    /// <summary>
    /// Creates a new Lane for an Objective.
    /// </summary>
    public async Task<LaneOperationResultDto?> CreateLaneAsync(
        int objectiveId,
        int displayOrder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new PoTool.Client.ApiClient.CreateLaneCommand
            {
                ObjectiveId = objectiveId,
                DisplayOrder = displayOrder
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LaneOperationResultDto>(
                await _releasePlanningClient.CreateLaneAsync(command, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error creating Lane");
            return null;
        }
    }

    /// <summary>
    /// Deletes a Lane and all its placements.
    /// </summary>
    public async Task<LaneOperationResultDto?> DeleteLaneAsync(
        int laneId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LaneOperationResultDto>(
                await _releasePlanningClient.DeleteLaneAsync(laneId, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error deleting Lane {LaneId}", laneId);
            return null;
        }
    }

    #endregion

    #region Epic Placement Operations

    /// <summary>
    /// Places an Epic on the board.
    /// </summary>
    public async Task<EpicPlacementResultDto?> CreatePlacementAsync(
        int epicId,
        int laneId,
        int rowIndex,
        int orderInRow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new PoTool.Client.ApiClient.CreateEpicPlacementCommand
            {
                EpicId = epicId,
                LaneId = laneId,
                RowIndex = rowIndex,
                OrderInRow = orderInRow
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<EpicPlacementResultDto>(
                await _releasePlanningClient.CreatePlacementAsync(command, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error creating Epic placement");
            return null;
        }
    }

    /// <summary>
    /// Moves an Epic to a different row.
    /// </summary>
    public async Task<EpicPlacementResultDto?> MoveEpicAsync(
        int placementId,
        int newRowIndex,
        int newOrderInRow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PoTool.Client.ApiClient.MoveEpicRequest
            {
                NewRowIndex = newRowIndex,
                NewOrderInRow = newOrderInRow
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<EpicPlacementResultDto>(
                await _releasePlanningClient.MoveEpicAsync(placementId, request, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error moving Epic placement {PlacementId}", placementId);
            return null;
        }
    }

    /// <summary>
    /// Reorders Epics within a row.
    /// </summary>
    public async Task<EpicPlacementResultDto?> ReorderEpicsInRowAsync(
        int laneId,
        int rowIndex,
        IReadOnlyList<int> placementIdsInOrder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new PoTool.Client.ApiClient.ReorderEpicsInRowCommand
            {
                LaneId = laneId,
                RowIndex = rowIndex,
                PlacementIdsInOrder = placementIdsInOrder.ToList()
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<EpicPlacementResultDto>(
                await _releasePlanningClient.ReorderEpicsInRowAsync(command, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error reordering Epics in row");
            return null;
        }
    }

    /// <summary>
    /// Deletes an Epic placement from the board (returns it to unplanned list).
    /// </summary>
    public async Task<EpicPlacementResultDto?> DeletePlacementAsync(
        int placementId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<EpicPlacementResultDto>(
                await _releasePlanningClient.DeletePlacementAsync(placementId, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error deleting Epic placement {PlacementId}", placementId);
            return null;
        }
    }

    #endregion

    #region Line Operations

    /// <summary>
    /// Creates a new Milestone Line.
    /// </summary>
    public async Task<LineOperationResultDto?> CreateMilestoneLineAsync(
        string label,
        double verticalPosition,
        MilestoneType type,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new PoTool.Client.ApiClient.CreateMilestoneLineCommand
            {
                Label = label,
                VerticalPosition = verticalPosition,
                Type = type
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LineOperationResultDto>(
                await _releasePlanningClient.CreateMilestoneLineAsync(command, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error creating Milestone Line");
            return null;
        }
    }

    /// <summary>
    /// Updates a Milestone Line.
    /// </summary>
    public async Task<LineOperationResultDto?> UpdateMilestoneLineAsync(
        int lineId,
        string label,
        double verticalPosition,
        MilestoneType type,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PoTool.Client.ApiClient.UpdateMilestoneLineRequest
            {
                Label = label,
                VerticalPosition = verticalPosition,
                Type = type
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LineOperationResultDto>(
                await _releasePlanningClient.UpdateMilestoneLineAsync(lineId, request, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error updating Milestone Line {LineId}", lineId);
            return null;
        }
    }

    /// <summary>
    /// Deletes a Milestone Line.
    /// </summary>
    public async Task<LineOperationResultDto?> DeleteMilestoneLineAsync(
        int lineId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LineOperationResultDto>(
                await _releasePlanningClient.DeleteMilestoneLineAsync(lineId, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error deleting Milestone Line {LineId}", lineId);
            return null;
        }
    }

    /// <summary>
    /// Creates a new Iteration Line.
    /// </summary>
    public async Task<LineOperationResultDto?> CreateIterationLineAsync(
        string label,
        double verticalPosition,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new PoTool.Client.ApiClient.CreateIterationLineCommand
            {
                Label = label,
                VerticalPosition = verticalPosition
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LineOperationResultDto>(
                await _releasePlanningClient.CreateIterationLineAsync(command, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error creating Iteration Line");
            return null;
        }
    }

    /// <summary>
    /// Updates an Iteration Line.
    /// </summary>
    public async Task<LineOperationResultDto?> UpdateIterationLineAsync(
        int lineId,
        string label,
        double verticalPosition,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PoTool.Client.ApiClient.UpdateIterationLineRequest
            {
                Label = label,
                VerticalPosition = verticalPosition
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LineOperationResultDto>(
                await _releasePlanningClient.UpdateIterationLineAsync(lineId, request, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error updating Iteration Line {LineId}", lineId);
            return null;
        }
    }

    /// <summary>
    /// Deletes an Iteration Line.
    /// </summary>
    public async Task<LineOperationResultDto?> DeleteIterationLineAsync(
        int lineId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<LineOperationResultDto>(
                await _releasePlanningClient.DeleteIterationLineAsync(lineId, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error deleting Iteration Line {LineId}", lineId);
            return null;
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Refreshes the validation cache for all Epics on the board.
    /// </summary>
    public async Task<ValidationCacheResultDto?> RefreshValidationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<ValidationCacheResultDto>(
                await _releasePlanningClient.RefreshValidationAsync(cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error refreshing validation cache");
            return null;
        }
    }

    #endregion

    #region Export Operations

    /// <summary>
    /// Exports the Release Planning Board.
    /// </summary>
    public async Task<ExportResultDto?> ExportBoardAsync(
        ExportOptionsDto options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<ExportResultDto>(
                await _releasePlanningClient.ExportBoardAsync(options, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error exporting Release Planning Board");
            return null;
        }
    }

    #endregion

    #region Epic Split Operations

    /// <summary>
    /// Gets all Features for a specific Epic (for split dialog).
    /// </summary>
    public async Task<IReadOnlyList<EpicFeatureDto>?> GetEpicFeaturesAsync(
        int epicId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault(
                await _releasePlanningClient.GetEpicFeaturesAsync(epicId, cancellationToken),
                static data => data.ToReadOnlyList(),
                Array.Empty<EpicFeatureDto>());
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error retrieving Features for Epic {EpicId}", epicId);
            return null;
        }
    }

    /// <summary>
    /// Splits an Epic into two Epics.
    /// </summary>
    public async Task<EpicSplitResultDto?> SplitEpicAsync(
        int epicId,
        string extractedEpicTitle,
        IReadOnlyList<int> featureIdsForExtractedEpic,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PoTool.Client.ApiClient.SplitEpicRequest
            {
                ExtractedEpicTitle = extractedEpicTitle,
                FeatureIdsForExtractedEpic = featureIdsForExtractedEpic.ToList()
            };
            return GeneratedCacheEnvelopeHelper.GetDataOrDefault<EpicSplitResultDto>(
                await _releasePlanningClient.SplitEpicAsync(epicId, request, cancellationToken));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Error splitting Epic {EpicId}", epicId);
            return null;
        }
    }

    #endregion
}
