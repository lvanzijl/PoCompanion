using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Commands;

/// <summary>
/// Command to update the Planning Board scope settings.
/// </summary>
/// <param name="ProductOwnerId">The Product Owner ID.</param>
/// <param name="Scope">The new scope setting.</param>
/// <param name="SelectedProductId">When scope is SingleProduct, which product.</param>
public sealed record UpdateBoardScopeCommand(
    int ProductOwnerId,
    BoardScope Scope,
    int? SelectedProductId = null) : ICommand<BoardOperationResultDto>;
