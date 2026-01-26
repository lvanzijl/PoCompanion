using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to initialize the default board layout.
/// Creates 3 rows + Iteration line + 3 rows + Release line + 3 rows.
/// </summary>
/// <param name="ProductOwnerId">The Product Owner ID.</param>
public sealed record InitializeDefaultBoardCommand(
    int ProductOwnerId) : ICommand<BoardOperationResultDto>;
