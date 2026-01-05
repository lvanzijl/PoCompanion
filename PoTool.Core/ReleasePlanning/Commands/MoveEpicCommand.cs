using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to move an Epic to a different row within the same Lane.
/// Cross-lane moves are forbidden by spec.
/// </summary>
/// <param name="PlacementId">The placement ID to move.</param>
/// <param name="NewRowIndex">The target row index.</param>
/// <param name="NewOrderInRow">The target order within the row.</param>
public sealed record MoveEpicCommand(
    int PlacementId,
    int NewRowIndex,
    int NewOrderInRow) : ICommand<EpicPlacementResultDto>;
