using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to link a team to a product.
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="TeamId">Team ID</param>
public sealed record LinkTeamToProductCommand(
    int ProductId,
    int TeamId
) : ICommand<bool>;
