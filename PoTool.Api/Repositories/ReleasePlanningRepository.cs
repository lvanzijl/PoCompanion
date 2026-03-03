using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.ReleasePlanning;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for Release Planning Board persistence.
/// </summary>
public class ReleasePlanningRepository : IReleasePlanningRepository
{
    private readonly PoToolDbContext _context;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ILogger<ReleasePlanningRepository> _logger;

    public ReleasePlanningRepository(
        PoToolDbContext context,
        IWorkItemRepository workItemRepository,
        IWorkItemStateClassificationService stateClassificationService,
        ILogger<ReleasePlanningRepository> logger)
    {
        _context = context;
        _workItemRepository = workItemRepository;
        _stateClassificationService = stateClassificationService;
        _logger = logger;
    }

    #region Lane Operations

    public async Task<IReadOnlyList<LaneDto>> GetAllLanesAsync(CancellationToken cancellationToken = default)
    {
        var lanes = await _context.Lanes
            .OrderBy(l => l.DisplayOrder)
            .ToListAsync(cancellationToken);

        var result = new List<LaneDto>();
        foreach (var lane in lanes)
        {
            var objective = await _workItemRepository.GetByTfsIdAsync(lane.ObjectiveId, cancellationToken);
            result.Add(new LaneDto
            {
                Id = lane.Id,
                ObjectiveId = lane.ObjectiveId,
                ObjectiveTitle = objective?.Title ?? $"Objective {lane.ObjectiveId}",
                DisplayOrder = lane.DisplayOrder
            });
        }

        return result;
    }

    public async Task<LaneDto?> GetLaneByIdAsync(int laneId, CancellationToken cancellationToken = default)
    {
        var lane = await _context.Lanes.FindAsync([laneId], cancellationToken);
        if (lane == null) return null;

        var objective = await _workItemRepository.GetByTfsIdAsync(lane.ObjectiveId, cancellationToken);
        return new LaneDto
        {
            Id = lane.Id,
            ObjectiveId = lane.ObjectiveId,
            ObjectiveTitle = objective?.Title ?? $"Objective {lane.ObjectiveId}",
            DisplayOrder = lane.DisplayOrder
        };
    }

    public async Task<LaneDto?> GetLaneByObjectiveIdAsync(int objectiveId, CancellationToken cancellationToken = default)
    {
        var lane = await _context.Lanes
            .OrderBy(l => l.Id)
            .FirstOrDefaultAsync(l => l.ObjectiveId == objectiveId, cancellationToken);

        if (lane == null) return null;

        var objective = await _workItemRepository.GetByTfsIdAsync(lane.ObjectiveId, cancellationToken);
        return new LaneDto
        {
            Id = lane.Id,
            ObjectiveId = lane.ObjectiveId,
            ObjectiveTitle = objective?.Title ?? $"Objective {lane.ObjectiveId}",
            DisplayOrder = lane.DisplayOrder
        };
    }

    public async Task<int> CreateLaneAsync(int objectiveId, int displayOrder, CancellationToken cancellationToken = default)
    {
        var lane = new LaneEntity
        {
            ObjectiveId = objectiveId,
            DisplayOrder = displayOrder
        };

        _context.Lanes.Add(lane);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created lane {LaneId} for objective {ObjectiveId}", lane.Id, objectiveId);
        return lane.Id;
    }

    public async Task<bool> DeleteLaneAsync(int laneId, CancellationToken cancellationToken = default)
    {
        var lane = await _context.Lanes.FindAsync([laneId], cancellationToken);
        if (lane == null) return false;

        _context.Lanes.Remove(lane);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted lane {LaneId}", laneId);
        return true;
    }

    #endregion

    #region Epic Placement Operations

    public async Task<IReadOnlyList<EpicPlacementDto>> GetAllPlacementsAsync(CancellationToken cancellationToken = default)
    {
        var placements = await _context.EpicPlacements
            .OrderBy(p => p.LaneId)
            .ThenBy(p => p.RowIndex)
            .ThenBy(p => p.OrderInRow)
            .ToListAsync(cancellationToken);

        return await MapPlacementsAsync(placements, cancellationToken);
    }

    public async Task<IReadOnlyList<EpicPlacementDto>> GetPlacementsByLaneIdAsync(int laneId, CancellationToken cancellationToken = default)
    {
        var placements = await _context.EpicPlacements
            .Where(p => p.LaneId == laneId)
            .OrderBy(p => p.RowIndex)
            .ThenBy(p => p.OrderInRow)
            .ToListAsync(cancellationToken);

        return await MapPlacementsAsync(placements, cancellationToken);
    }

    public async Task<EpicPlacementDto?> GetPlacementByIdAsync(int placementId, CancellationToken cancellationToken = default)
    {
        var placement = await _context.EpicPlacements.FindAsync([placementId], cancellationToken);
        if (placement == null) return null;

        return (await MapPlacementsAsync([placement], cancellationToken)).FirstOrDefault();
    }

    public async Task<EpicPlacementDto?> GetPlacementByEpicIdAsync(int epicId, CancellationToken cancellationToken = default)
    {
        var placement = await _context.EpicPlacements
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(p => p.EpicId == epicId, cancellationToken);

        if (placement == null) return null;

        return (await MapPlacementsAsync([placement], cancellationToken)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<int>> GetPlacedEpicIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EpicPlacements
            .Select(p => p.EpicId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreatePlacementAsync(int epicId, int laneId, int rowIndex, int orderInRow, CancellationToken cancellationToken = default)
    {
        var placement = new EpicPlacementEntity
        {
            EpicId = epicId,
            LaneId = laneId,
            RowIndex = rowIndex,
            OrderInRow = orderInRow
        };

        _context.EpicPlacements.Add(placement);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created placement {PlacementId} for epic {EpicId} at ({LaneId}, {RowIndex}, {OrderInRow})",
            placement.Id, epicId, laneId, rowIndex, orderInRow);
        return placement.Id;
    }

    public async Task<bool> UpdatePlacementAsync(int placementId, int rowIndex, int orderInRow, CancellationToken cancellationToken = default)
    {
        var placement = await _context.EpicPlacements.FindAsync([placementId], cancellationToken);
        if (placement == null) return false;

        placement.RowIndex = rowIndex;
        placement.OrderInRow = orderInRow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated placement {PlacementId} to ({RowIndex}, {OrderInRow})", placementId, rowIndex, orderInRow);
        return true;
    }

    public async Task<bool> DeletePlacementAsync(int placementId, CancellationToken cancellationToken = default)
    {
        var placement = await _context.EpicPlacements.FindAsync([placementId], cancellationToken);
        if (placement == null) return false;

        _context.EpicPlacements.Remove(placement);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted placement {PlacementId}", placementId);
        return true;
    }

    public async Task<bool> DeletePlacementsByLaneIdAsync(int laneId, CancellationToken cancellationToken = default)
    {
        var placements = await _context.EpicPlacements
            .Where(p => p.LaneId == laneId)
            .ToListAsync(cancellationToken);

        _context.EpicPlacements.RemoveRange(placements);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} placements for lane {LaneId}", placements.Count, laneId);
        return true;
    }

    private async Task<IReadOnlyList<EpicPlacementDto>> MapPlacementsAsync(
        IEnumerable<EpicPlacementEntity> placements,
        CancellationToken cancellationToken)
    {
        var result = new List<EpicPlacementDto>();

        foreach (var placement in placements)
        {
            var epic = await _workItemRepository.GetByTfsIdAsync(placement.EpicId, cancellationToken);
            var validation = await GetCachedValidationAsync(placement.EpicId, cancellationToken);

            // Get state classification
            var stateClassification = StateClassification.New;
            if (epic != null)
            {
                stateClassification = await _stateClassificationService.GetClassificationAsync(
                    epic.Type, epic.State, cancellationToken);
            }

            result.Add(new EpicPlacementDto
            {
                Id = placement.Id,
                EpicId = placement.EpicId,
                EpicTitle = epic?.Title ?? $"Epic {placement.EpicId}",
                LaneId = placement.LaneId,
                RowIndex = placement.RowIndex,
                OrderInRow = placement.OrderInRow,
                Effort = epic?.Effort,
                State = epic?.State ?? string.Empty,
                ValidationIndicator = validation,
                StateClassification = stateClassification
            });
        }

        return result;
    }

    #endregion

    #region Milestone Line Operations

    public async Task<IReadOnlyList<MilestoneLineDto>> GetAllMilestoneLinesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MilestoneLines
            .OrderBy(l => l.VerticalPosition)
            .Select(l => new MilestoneLineDto
            {
                Id = l.Id,
                Label = l.Label,
                VerticalPosition = l.VerticalPosition,
                Type = (MilestoneType)l.Type
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MilestoneLineDto?> GetMilestoneLineByIdAsync(int lineId, CancellationToken cancellationToken = default)
    {
        var line = await _context.MilestoneLines.FindAsync([lineId], cancellationToken);
        if (line == null) return null;

        return new MilestoneLineDto
        {
            Id = line.Id,
            Label = line.Label,
            VerticalPosition = line.VerticalPosition,
            Type = (MilestoneType)line.Type
        };
    }

    public async Task<int> CreateMilestoneLineAsync(string label, double verticalPosition, MilestoneType type, CancellationToken cancellationToken = default)
    {
        var line = new MilestoneLineEntity
        {
            Label = label,
            VerticalPosition = verticalPosition,
            Type = (int)type
        };

        _context.MilestoneLines.Add(line);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created milestone line {LineId} at position {Position}", line.Id, verticalPosition);
        return line.Id;
    }

    public async Task<bool> UpdateMilestoneLineAsync(int lineId, string label, double verticalPosition, MilestoneType type, CancellationToken cancellationToken = default)
    {
        var line = await _context.MilestoneLines.FindAsync([lineId], cancellationToken);
        if (line == null) return false;

        line.Label = label;
        line.VerticalPosition = verticalPosition;
        line.Type = (int)type;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated milestone line {LineId}", lineId);
        return true;
    }

    public async Task<bool> DeleteMilestoneLineAsync(int lineId, CancellationToken cancellationToken = default)
    {
        var line = await _context.MilestoneLines.FindAsync([lineId], cancellationToken);
        if (line == null) return false;

        _context.MilestoneLines.Remove(line);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted milestone line {LineId}", lineId);
        return true;
    }

    #endregion

    #region Iteration Line Operations

    public async Task<IReadOnlyList<IterationLineDto>> GetAllIterationLinesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.IterationLines
            .OrderBy(l => l.VerticalPosition)
            .Select(l => new IterationLineDto
            {
                Id = l.Id,
                Label = l.Label,
                VerticalPosition = l.VerticalPosition
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IterationLineDto?> GetIterationLineByIdAsync(int lineId, CancellationToken cancellationToken = default)
    {
        var line = await _context.IterationLines.FindAsync([lineId], cancellationToken);
        if (line == null) return null;

        return new IterationLineDto
        {
            Id = line.Id,
            Label = line.Label,
            VerticalPosition = line.VerticalPosition
        };
    }

    public async Task<int> CreateIterationLineAsync(string label, double verticalPosition, CancellationToken cancellationToken = default)
    {
        var line = new IterationLineEntity
        {
            Label = label,
            VerticalPosition = verticalPosition
        };

        _context.IterationLines.Add(line);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created iteration line {LineId} at position {Position}", line.Id, verticalPosition);
        return line.Id;
    }

    public async Task<bool> UpdateIterationLineAsync(int lineId, string label, double verticalPosition, CancellationToken cancellationToken = default)
    {
        var line = await _context.IterationLines.FindAsync([lineId], cancellationToken);
        if (line == null) return false;

        line.Label = label;
        line.VerticalPosition = verticalPosition;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated iteration line {LineId}", lineId);
        return true;
    }

    public async Task<bool> DeleteIterationLineAsync(int lineId, CancellationToken cancellationToken = default)
    {
        var line = await _context.IterationLines.FindAsync([lineId], cancellationToken);
        if (line == null) return false;

        _context.IterationLines.Remove(line);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted iteration line {LineId}", lineId);
        return true;
    }

    #endregion

    #region Validation Cache Operations

    public async Task<ValidationIndicator> GetCachedValidationAsync(int epicId, CancellationToken cancellationToken = default)
    {
        var cached = await _context.CachedValidationResults
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(c => c.EpicId == epicId, cancellationToken);

        return cached != null ? (ValidationIndicator)cached.Indicator : ValidationIndicator.None;
    }

    public async Task UpdateCachedValidationAsync(int epicId, ValidationIndicator indicator, CancellationToken cancellationToken = default)
    {
        var cached = await _context.CachedValidationResults
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(c => c.EpicId == epicId, cancellationToken);

        if (cached == null)
        {
            cached = new CachedValidationResultEntity
            {
                EpicId = epicId,
                Indicator = (int)indicator,
                LastUpdated = DateTimeOffset.UtcNow
            };
            _context.CachedValidationResults.Add(cached);
        }
        else
        {
            cached.Indicator = (int)indicator;
            cached.LastUpdated = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearValidationCacheAsync(CancellationToken cancellationToken = default)
    {
        var allCached = await _context.CachedValidationResults.ToListAsync(cancellationToken);
        _context.CachedValidationResults.RemoveRange(allCached);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Cleared validation cache ({Count} entries)", allCached.Count);
    }

    #endregion
}
