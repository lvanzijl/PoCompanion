using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to create a Lane (Objective) on the Release Planning Board.
/// </summary>
/// <param name="ObjectiveId">The TFS ID of the Objective.</param>
/// <param name="DisplayOrder">The display order (left to right).</param>
public sealed record CreateLaneCommand(
    int ObjectiveId,
    int DisplayOrder) : ICommand<LaneOperationResultDto>;
