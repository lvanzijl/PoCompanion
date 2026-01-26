using System.Net.Http.Json;
using PoTool.Shared.Planning;

namespace PoTool.Client.Services;

/// <summary>
/// Service for new Planning Board operations.
/// </summary>
public class PlanningBoardService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlanningBoardService> _logger;

    public PlanningBoardService(
        HttpClient httpClient,
        ILogger<PlanningBoardService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the complete Planning Board state.
    /// </summary>
    public async Task<PlanningBoardDto?> GetBoardAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PlanningBoardDto>(
                $"api/planning/board/{productOwnerId}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Planning Board for Product Owner {ProductOwnerId}", productOwnerId);
            return null;
        }
    }

    /// <summary>
    /// Gets unplanned Epics.
    /// </summary>
    public async Task<IReadOnlyList<UnplannedEpicDto>?> GetUnplannedEpicsAsync(
        int productOwnerId,
        int? productId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/planning/unplanned-epics/{productOwnerId}";
            if (productId.HasValue)
            {
                url += $"?productId={productId}";
            }

            return await _httpClient.GetFromJsonAsync<IReadOnlyList<UnplannedEpicDto>>(
                url,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unplanned Epics for Product Owner {ProductOwnerId}", productOwnerId);
            return null;
        }
    }

    /// <summary>
    /// Initializes the default board layout.
    /// </summary>
    public async Task<BoardOperationResultDto?> InitializeBoardAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"api/planning/board/{productOwnerId}/initialize",
                null,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<BoardOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Planning Board for Product Owner {ProductOwnerId}", productOwnerId);
            return new BoardOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    #region Row Operations

    /// <summary>
    /// Creates a new row.
    /// </summary>
    public async Task<RowOperationResultDto?> CreateRowAsync(
        CreateRowRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/planning/rows",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<RowOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create row");
            return new RowOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    /// <summary>
    /// Creates a marker row.
    /// </summary>
    public async Task<RowOperationResultDto?> CreateMarkerRowAsync(
        CreateMarkerRowRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/planning/rows/marker",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<RowOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create marker row");
            return new RowOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    /// <summary>
    /// Deletes a row.
    /// </summary>
    public async Task<RowOperationResultDto?> DeleteRowAsync(
        int rowId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"api/planning/rows/{rowId}",
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<RowOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete row {RowId}", rowId);
            return new RowOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    /// <summary>
    /// Moves a row to a new position.
    /// </summary>
    public async Task<RowOperationResultDto?> MoveRowAsync(
        int rowId,
        MoveRowRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/planning/rows/{rowId}/move",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<RowOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move row {RowId}", rowId);
            return new RowOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    #endregion

    #region Epic Placement Operations

    /// <summary>
    /// Places an Epic on the board.
    /// </summary>
    public async Task<PlacementOperationResultDto?> CreatePlacementAsync(
        CreateEpicPlacementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/planning/placements",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<PlacementOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Epic placement");
            return new PlacementOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    /// <summary>
    /// Moves an Epic placement.
    /// </summary>
    public async Task<PlacementOperationResultDto?> MovePlacementAsync(
        int placementId,
        MovePlanningEpicRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/planning/placements/{placementId}/move",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<PlacementOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move Epic placement {PlacementId}", placementId);
            return new PlacementOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    /// <summary>
    /// Deletes Epic placements.
    /// </summary>
    public async Task<PlacementOperationResultDto?> DeletePlacementsAsync(
        IReadOnlyList<int> placementIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { PlacementIds = placementIds };
            var response = await _httpClient.PostAsJsonAsync(
                "api/planning/placements/delete",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<PlacementOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Epic placements");
            return new PlacementOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    #endregion

    #region Board Settings

    /// <summary>
    /// Updates the board scope.
    /// </summary>
    public async Task<BoardOperationResultDto?> UpdateScopeAsync(
        int productOwnerId,
        UpdateBoardScopeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/planning/board/{productOwnerId}/scope",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<BoardOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update board scope for Product Owner {ProductOwnerId}", productOwnerId);
            return new BoardOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    /// <summary>
    /// Updates product visibility.
    /// </summary>
    public async Task<BoardOperationResultDto?> UpdateProductVisibilityAsync(
        int productOwnerId,
        UpdateProductVisibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/planning/board/{productOwnerId}/visibility",
                request,
                cancellationToken);

            return await response.Content.ReadFromJsonAsync<BoardOperationResultDto>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product visibility for Product Owner {ProductOwnerId}", productOwnerId);
            return new BoardOperationResultDto { Success = false, ErrorMessage = "Operation failed. Please try again." };
        }
    }

    #endregion
}
