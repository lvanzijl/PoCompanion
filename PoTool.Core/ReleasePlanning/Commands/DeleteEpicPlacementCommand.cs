using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to delete an Epic's placement from the Release Planning Board.
/// Note: Dragging Epics back to the Unplanned list is forbidden by spec.
/// This command is for internal use only (e.g., cleanup).
/// </summary>
/// <param name="PlacementId">The placement ID to delete.</param>
public sealed record DeleteEpicPlacementCommand(int PlacementId) : ICommand<EpicPlacementResultDto>;
