using PoTool.Shared.ReleasePlanning;

namespace PoTool.Api.Services;

/// <summary>
/// Service for deriving connectors between Epics on the Release Planning Board.
/// Connectors represent ordering flow (git-style) and are computed, not persisted.
/// </summary>
public class ConnectorDerivationService
{
    /// <summary>
    /// Derives connectors for a single lane based on Epic placements.
    /// </summary>
    /// <param name="placements">The placements in the lane, ordered by RowIndex and OrderInRow.</param>
    /// <returns>List of derived connectors.</returns>
    public IReadOnlyList<ConnectorDto> DeriveConnectorsForLane(IReadOnlyList<EpicPlacementDto> placements)
    {
        if (placements.Count == 0) return [];

        var connectors = new List<ConnectorDto>();
        
        // Group placements by RowIndex
        var rowGroups = placements
            .GroupBy(p => p.RowIndex)
            .OrderBy(g => g.Key)
            .ToList();

        // For each adjacent row pair, determine connector types
        for (int i = 0; i < rowGroups.Count - 1; i++)
        {
            var currentRow = rowGroups[i].OrderBy(p => p.OrderInRow).ToList();
            var nextRow = rowGroups[i + 1].OrderBy(p => p.OrderInRow).ToList();

            var rowConnectors = DeriveConnectorsBetweenRows(currentRow, nextRow);
            connectors.AddRange(rowConnectors);
        }

        return connectors;
    }

    /// <summary>
    /// Derives connectors for all lanes on the board.
    /// </summary>
    /// <param name="lanes">All lanes on the board.</param>
    /// <param name="placements">All placements on the board.</param>
    /// <returns>List of all derived connectors.</returns>
    public IReadOnlyList<ConnectorDto> DeriveAllConnectors(
        IReadOnlyList<LaneDto> lanes,
        IReadOnlyList<EpicPlacementDto> placements)
    {
        var connectors = new List<ConnectorDto>();

        foreach (var lane in lanes)
        {
            var lanePlacements = placements
                .Where(p => p.LaneId == lane.Id)
                .OrderBy(p => p.RowIndex)
                .ThenBy(p => p.OrderInRow)
                .ToList();

            var laneConnectors = DeriveConnectorsForLane(lanePlacements);
            connectors.AddRange(laneConnectors);
        }

        return connectors;
    }

    private static IReadOnlyList<ConnectorDto> DeriveConnectorsBetweenRows(
        IReadOnlyList<EpicPlacementDto> currentRow,
        IReadOnlyList<EpicPlacementDto> nextRow)
    {
        var connectors = new List<ConnectorDto>();
        int countA = currentRow.Count;
        int countB = nextRow.Count;

        // Determine connector type based on the derivation algorithm in the spec
        // If |A| = 1 and |B| > 1: split
        // If |A| > 1 and |B| = 1: merge
        // If |A| = |B|: parallel continuation
        // Draw connectors from all A → all B as required

        if (countA == 1 && countB > 1)
        {
            // Split: single source to multiple targets
            foreach (var target in nextRow)
            {
                connectors.Add(new ConnectorDto
                {
                    SourcePlacementId = currentRow[0].Id,
                    TargetPlacementId = target.Id,
                    Type = ConnectorType.Split
                });
            }
        }
        else if (countA > 1 && countB == 1)
        {
            // Merge: multiple sources to single target
            foreach (var source in currentRow)
            {
                connectors.Add(new ConnectorDto
                {
                    SourcePlacementId = source.Id,
                    TargetPlacementId = nextRow[0].Id,
                    Type = ConnectorType.Merge
                });
            }
        }
        else if (countA == countB)
        {
            // Parallel continuation: 1:1 mapping based on order
            for (int j = 0; j < countA; j++)
            {
                connectors.Add(new ConnectorDto
                {
                    SourcePlacementId = currentRow[j].Id,
                    TargetPlacementId = nextRow[j].Id,
                    Type = countA == 1 ? ConnectorType.Direct : ConnectorType.Parallel
                });
            }
        }
        else
        {
            // Mixed case: all sources connect to all targets
            // This handles the general case when counts don't match cleanly
            foreach (var source in currentRow)
            {
                foreach (var target in nextRow)
                {
                    connectors.Add(new ConnectorDto
                    {
                        SourcePlacementId = source.Id,
                        TargetPlacementId = target.Id,
                        Type = countA < countB ? ConnectorType.Split : ConnectorType.Merge
                    });
                }
            }
        }

        return connectors;
    }
}
