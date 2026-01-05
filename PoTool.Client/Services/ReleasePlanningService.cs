using System.Net.Http.Json;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Client.Services;

// Client-side request DTOs for API calls
public record CreateLaneCommand(int ObjectiveId, int DisplayOrder);
public record CreateEpicPlacementCommand(int EpicId, int LaneId, int RowIndex, int OrderInRow);
public record ReorderEpicsInRowCommand(int LaneId, int RowIndex, IReadOnlyList<int> PlacementIdsInOrder);
public record CreateMilestoneLineCommand(string Label, double VerticalPosition, MilestoneType Type);
public record CreateIterationLineCommand(string Label, double VerticalPosition);
public record MoveEpicRequest(int NewRowIndex, int NewOrderInRow);

/// <summary>
/// Service for Release Planning Board operations.
/// </summary>
public class ReleasePlanningService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReleasePlanningService> _logger;

    public ReleasePlanningService(
        HttpClient httpClient,
        ILogger<ReleasePlanningService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the complete Release Planning Board state.
    /// </summary>
    public async Task<ReleasePlanningBoardDto?> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ReleasePlanningBoardDto>(
                "api/releaseplanning/board", 
                cancellationToken);
        }
        catch (Exception ex)
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
            return await _httpClient.GetFromJsonAsync<List<UnplannedEpicDto>>(
                "api/releaseplanning/unplanned-epics", 
                cancellationToken);
        }
        catch (Exception ex)
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
            return await _httpClient.GetFromJsonAsync<List<ObjectiveEpicDto>>(
                $"api/releaseplanning/objectives/{objectiveId}/epics", 
                cancellationToken);
        }
        catch (Exception ex)
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
            var command = new CreateLaneCommand(objectiveId, displayOrder);
            var response = await _httpClient.PostAsJsonAsync("api/releaseplanning/lanes", command, cancellationToken);
            return await response.Content.ReadFromJsonAsync<LaneOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            var response = await _httpClient.DeleteAsync($"api/releaseplanning/lanes/{laneId}", cancellationToken);
            return await response.Content.ReadFromJsonAsync<LaneOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            var command = new CreateEpicPlacementCommand(epicId, laneId, rowIndex, orderInRow);
            var response = await _httpClient.PostAsJsonAsync("api/releaseplanning/placements", command, cancellationToken);
            return await response.Content.ReadFromJsonAsync<EpicPlacementResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            var request = new MoveEpicRequest(newRowIndex, newOrderInRow);
            var response = await _httpClient.PostAsJsonAsync(
                $"api/releaseplanning/placements/{placementId}/move", 
                request, 
                cancellationToken);
            return await response.Content.ReadFromJsonAsync<EpicPlacementResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            var command = new ReorderEpicsInRowCommand(laneId, rowIndex, placementIdsInOrder);
            var response = await _httpClient.PostAsJsonAsync("api/releaseplanning/rows/reorder", command, cancellationToken);
            return await response.Content.ReadFromJsonAsync<EpicPlacementResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering Epics in row");
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
            var command = new CreateMilestoneLineCommand(label, verticalPosition, type);
            var response = await _httpClient.PostAsJsonAsync("api/releaseplanning/milestone-lines", command, cancellationToken);
            return await response.Content.ReadFromJsonAsync<LineOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Milestone Line");
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
            var response = await _httpClient.DeleteAsync($"api/releaseplanning/milestone-lines/{lineId}", cancellationToken);
            return await response.Content.ReadFromJsonAsync<LineOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            var command = new CreateIterationLineCommand(label, verticalPosition);
            var response = await _httpClient.PostAsJsonAsync("api/releaseplanning/iteration-lines", command, cancellationToken);
            return await response.Content.ReadFromJsonAsync<LineOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Iteration Line");
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
            var response = await _httpClient.DeleteAsync($"api/releaseplanning/iteration-lines/{lineId}", cancellationToken);
            return await response.Content.ReadFromJsonAsync<LineOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            var response = await _httpClient.PostAsync("api/releaseplanning/validation/refresh", null, cancellationToken);
            return await response.Content.ReadFromJsonAsync<ValidationCacheResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            var response = await _httpClient.PostAsJsonAsync("api/releaseplanning/export", options, cancellationToken);
            return await response.Content.ReadFromJsonAsync<ExportResultDto>(cancellationToken);
        }
        catch (Exception ex)
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
            return await _httpClient.GetFromJsonAsync<List<EpicFeatureDto>>(
                $"api/releaseplanning/epics/{epicId}/features",
                cancellationToken);
        }
        catch (Exception ex)
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
            var request = new SplitEpicRequest(extractedEpicTitle, featureIdsForExtractedEpic);
            var response = await _httpClient.PostAsJsonAsync(
                $"api/releaseplanning/epics/{epicId}/split",
                request,
                cancellationToken);
            
            // Try to deserialize the result even for non-success status (e.g., 400 BadRequest returns EpicSplitResultDto)
            var result = await response.Content.ReadFromJsonAsync<EpicSplitResultDto>(cancellationToken);
            
            // If we got a result, return it (it will have Success=false for failures)
            if (result != null)
            {
                return result;
            }
            
            // If deserialization failed but response was successful, something is wrong
            if (response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Successfully split Epic {EpicId} but response deserialization failed", epicId);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting Epic {EpicId}", epicId);
            return null;
        }
    }

    #endregion
}

// Client-side request DTO for Epic Split
public record SplitEpicRequest(string ExtractedEpicTitle, IReadOnlyList<int> FeatureIdsForExtractedEpic);
