using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to create a Milestone Line on the Release Planning Board.
/// </summary>
/// <param name="Label">The label for the milestone.</param>
/// <param name="VerticalPosition">The vertical position (between rows).</param>
/// <param name="Type">The milestone type.</param>
public sealed record CreateMilestoneLineCommand(
    string Label,
    double VerticalPosition,
    MilestoneType Type) : ICommand<LineOperationResultDto>;
