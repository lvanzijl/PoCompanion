using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for Release Planning Board persistence.
/// </summary>
public interface IReleasePlanningRepository
{
    // Lane operations
    Task<IReadOnlyList<LaneDto>> GetAllLanesAsync(CancellationToken cancellationToken = default);
    Task<LaneDto?> GetLaneByIdAsync(int laneId, CancellationToken cancellationToken = default);
    Task<LaneDto?> GetLaneByObjectiveIdAsync(int objectiveId, CancellationToken cancellationToken = default);
    Task<int> CreateLaneAsync(int objectiveId, int displayOrder, CancellationToken cancellationToken = default);
    Task<bool> DeleteLaneAsync(int laneId, CancellationToken cancellationToken = default);

    // Epic placement operations
    Task<IReadOnlyList<EpicPlacementDto>> GetAllPlacementsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EpicPlacementDto>> GetPlacementsByLaneIdAsync(int laneId, CancellationToken cancellationToken = default);
    Task<EpicPlacementDto?> GetPlacementByIdAsync(int placementId, CancellationToken cancellationToken = default);
    Task<EpicPlacementDto?> GetPlacementByEpicIdAsync(int epicId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetPlacedEpicIdsAsync(CancellationToken cancellationToken = default);
    Task<int> CreatePlacementAsync(int epicId, int laneId, int rowIndex, int orderInRow, CancellationToken cancellationToken = default);
    Task<bool> UpdatePlacementAsync(int placementId, int rowIndex, int orderInRow, CancellationToken cancellationToken = default);
    Task<bool> DeletePlacementAsync(int placementId, CancellationToken cancellationToken = default);
    Task<bool> DeletePlacementsByLaneIdAsync(int laneId, CancellationToken cancellationToken = default);

    // Milestone line operations
    Task<IReadOnlyList<MilestoneLineDto>> GetAllMilestoneLinesAsync(CancellationToken cancellationToken = default);
    Task<MilestoneLineDto?> GetMilestoneLineByIdAsync(int lineId, CancellationToken cancellationToken = default);
    Task<int> CreateMilestoneLineAsync(string label, double verticalPosition, MilestoneType type, CancellationToken cancellationToken = default);
    Task<bool> UpdateMilestoneLineAsync(int lineId, string label, double verticalPosition, MilestoneType type, CancellationToken cancellationToken = default);
    Task<bool> DeleteMilestoneLineAsync(int lineId, CancellationToken cancellationToken = default);

    // Iteration line operations
    Task<IReadOnlyList<IterationLineDto>> GetAllIterationLinesAsync(CancellationToken cancellationToken = default);
    Task<IterationLineDto?> GetIterationLineByIdAsync(int lineId, CancellationToken cancellationToken = default);
    Task<int> CreateIterationLineAsync(string label, double verticalPosition, CancellationToken cancellationToken = default);
    Task<bool> UpdateIterationLineAsync(int lineId, string label, double verticalPosition, CancellationToken cancellationToken = default);
    Task<bool> DeleteIterationLineAsync(int lineId, CancellationToken cancellationToken = default);

    // Validation cache operations
    Task<ValidationIndicator> GetCachedValidationAsync(int epicId, CancellationToken cancellationToken = default);
    Task UpdateCachedValidationAsync(int epicId, ValidationIndicator indicator, CancellationToken cancellationToken = default);
    Task ClearValidationCacheAsync(CancellationToken cancellationToken = default);
}
