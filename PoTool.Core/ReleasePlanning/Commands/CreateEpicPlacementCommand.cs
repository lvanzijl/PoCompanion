using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to place an Epic on the Release Planning Board.
/// </summary>
/// <param name="EpicId">The TFS ID of the Epic to place.</param>
/// <param name="LaneId">The Lane ID where the Epic should be placed.</param>
/// <param name="RowIndex">The row index for the placement.</param>
/// <param name="OrderInRow">The order within the row (for parallel Epics).</param>
public sealed record CreateEpicPlacementCommand(
    int EpicId,
    int LaneId,
    int RowIndex,
    int OrderInRow) : ICommand<EpicPlacementResultDto>;
