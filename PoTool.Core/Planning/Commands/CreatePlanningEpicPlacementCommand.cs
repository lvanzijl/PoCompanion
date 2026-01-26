using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to place an epic on the Planning Board.
/// </summary>
/// <param name="EpicId">The TFS ID of the Epic to place.</param>
/// <param name="RowId">The row to place the epic in.</param>
/// <param name="OrderInCell">Order within the cell.</param>
public sealed record CreatePlanningEpicPlacementCommand(
    int EpicId,
    int RowId,
    int OrderInCell = 0) : ICommand<PlacementOperationResultDto>;
