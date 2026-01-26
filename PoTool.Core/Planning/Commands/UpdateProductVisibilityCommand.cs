using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to update product column visibility.
/// </summary>
/// <param name="ProductOwnerId">The Product Owner ID.</param>
/// <param name="ProductId">The Product ID to update.</param>
/// <param name="IsVisible">Whether the product column should be visible.</param>
public sealed record UpdateProductVisibilityCommand(
    int ProductOwnerId,
    int ProductId,
    bool IsVisible) : ICommand<BoardOperationResultDto>;
