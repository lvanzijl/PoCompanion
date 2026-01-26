using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to create a new row on the Planning Board.
/// </summary>
/// <param name="InsertBeforeRowId">Insert before this row ID. If null, insert at end.</param>
/// <param name="InsertBelow">If true, insert below the reference row instead of above.</param>
public sealed record CreateBoardRowCommand(
    int? InsertBeforeRowId = null,
    bool InsertBelow = false) : ICommand<RowOperationResultDto>;
