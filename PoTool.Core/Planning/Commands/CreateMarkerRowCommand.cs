using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to create a marker row (Iteration or Release line) on the Planning Board.
/// </summary>
/// <param name="MarkerType">Type of marker (Iteration or Release).</param>
/// <param name="Label">Label for the marker row.</param>
/// <param name="InsertBeforeRowId">Insert before this row ID. If null, insert at end.</param>
/// <param name="InsertBelow">If true, insert below the reference row instead of above.</param>
public sealed record CreateMarkerRowCommand(
    MarkerRowType MarkerType,
    string Label,
    int? InsertBeforeRowId = null,
    bool InsertBelow = false) : ICommand<RowOperationResultDto>;
