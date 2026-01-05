using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to reorder Epics within a single row.
/// </summary>
/// <param name="LaneId">The Lane ID containing the row.</param>
/// <param name="RowIndex">The row index to reorder.</param>
/// <param name="PlacementIdsInOrder">The placement IDs in their new order (left to right).</param>
public sealed record ReorderEpicsInRowCommand(
    int LaneId,
    int RowIndex,
    IReadOnlyList<int> PlacementIdsInOrder) : ICommand<EpicPlacementResultDto>;
