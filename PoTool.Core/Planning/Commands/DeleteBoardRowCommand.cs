using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to delete a board row.
/// Only succeeds if the row has no epic placements.
/// </summary>
/// <param name="RowId">The ID of the row to delete.</param>
public sealed record DeleteBoardRowCommand(int RowId) : ICommand<RowOperationResultDto>;
