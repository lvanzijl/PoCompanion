using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to update a marker row's label on the Planning Board.
/// </summary>
/// <param name="RowId">ID of the marker row to update.</param>
/// <param name="Label">New label for the marker row.</param>
public sealed record UpdateMarkerRowCommand(
    int RowId,
    string Label) : ICommand<RowOperationResultDto>;
