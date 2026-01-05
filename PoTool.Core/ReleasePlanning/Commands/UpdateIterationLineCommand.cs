using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to update an Iteration Line on the Release Planning Board.
/// </summary>
/// <param name="LineId">The line ID to update.</param>
/// <param name="Label">The new label.</param>
/// <param name="VerticalPosition">The new vertical position.</param>
public sealed record UpdateIterationLineCommand(
    int LineId,
    string Label,
    double VerticalPosition) : ICommand<LineOperationResultDto>;
