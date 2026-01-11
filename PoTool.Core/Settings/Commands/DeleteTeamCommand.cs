using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to permanently delete a team.
/// This will remove the team entity and all product-team links.
/// </summary>
/// <param name="Id">Team ID to delete</param>
public sealed record DeleteTeamCommand(int Id) : ICommand<bool>;
