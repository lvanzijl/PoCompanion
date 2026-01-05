using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to delete a Lane from the Release Planning Board.
/// All Epic placements in the Lane will also be deleted.
/// </summary>
/// <param name="LaneId">The lane ID to delete.</param>
public sealed record DeleteLaneCommand(int LaneId) : ICommand<LaneOperationResultDto>;
