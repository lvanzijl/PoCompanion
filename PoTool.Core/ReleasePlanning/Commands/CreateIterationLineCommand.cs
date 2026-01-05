using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to create an Iteration Line on the Release Planning Board.
/// </summary>
/// <param name="Label">The label for the iteration.</param>
/// <param name="VerticalPosition">The vertical position (between rows).</param>
public sealed record CreateIterationLineCommand(
    string Label,
    double VerticalPosition) : ICommand<LineOperationResultDto>;
