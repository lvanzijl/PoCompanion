using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to move a row to a new position on the Planning Board.
/// </summary>
/// <param name="RowId">ID of the row to move.</param>
/// <param name="TargetRowId">ID of the row to insert before/after.</param>
/// <param name="InsertBelow">If true, insert below the target row; otherwise insert above.</param>
public sealed record MoveRowCommand(
    int RowId,
    int TargetRowId,
    bool InsertBelow = false) : ICommand<RowOperationResultDto>;
