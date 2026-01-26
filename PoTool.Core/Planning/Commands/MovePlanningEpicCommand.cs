using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to move an epic placement to a different row.
/// </summary>
/// <param name="PlacementId">The ID of the placement to move.</param>
/// <param name="NewRowId">The new row ID.</param>
/// <param name="NewOrderInCell">New order within the cell.</param>
public sealed record MovePlanningEpicCommand(
    int PlacementId,
    int NewRowId,
    int NewOrderInCell) : ICommand<PlacementOperationResultDto>;
