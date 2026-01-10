using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to unlink a team from a product.
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="TeamId">Team ID</param>
public sealed record UnlinkTeamFromProductCommand(
    int ProductId,
    int TeamId
) : ICommand<bool>;
