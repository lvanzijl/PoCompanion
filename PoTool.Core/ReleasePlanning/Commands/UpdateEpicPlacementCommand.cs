using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to update an Epic's placement on the Release Planning Board.
/// </summary>
/// <param name="PlacementId">The placement ID to update.</param>
/// <param name="RowIndex">The new row index.</param>
/// <param name="OrderInRow">The new order within the row.</param>
public sealed record UpdateEpicPlacementCommand(
    int PlacementId,
    int RowIndex,
    int OrderInRow) : ICommand<EpicPlacementResultDto>;
