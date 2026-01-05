using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to update a Milestone Line on the Release Planning Board.
/// </summary>
/// <param name="LineId">The line ID to update.</param>
/// <param name="Label">The new label.</param>
/// <param name="VerticalPosition">The new vertical position.</param>
/// <param name="Type">The new milestone type.</param>
public sealed record UpdateMilestoneLineCommand(
    int LineId,
    string Label,
    double VerticalPosition,
    MilestoneType Type) : ICommand<LineOperationResultDto>;
