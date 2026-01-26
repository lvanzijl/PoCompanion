using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to delete epic placements from the board.
/// Returns the epics to the unplanned list.
/// </summary>
/// <param name="PlacementIds">The IDs of the placements to delete.</param>
public sealed record DeletePlanningEpicPlacementsCommand(
    IReadOnlyList<int> PlacementIds) : ICommand<PlacementOperationResultDto>;
