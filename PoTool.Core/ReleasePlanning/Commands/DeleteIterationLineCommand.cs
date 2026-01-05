using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to delete an Iteration Line from the Release Planning Board.
/// </summary>
/// <param name="LineId">The line ID to delete.</param>
public sealed record DeleteIterationLineCommand(int LineId) : ICommand<LineOperationResultDto>;
