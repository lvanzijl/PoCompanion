using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to permanently delete a team.
/// This will remove the team entity and all product-team links.
/// 
/// DEAD CODE WARNING: This command is unused. Only sent by obsolete TeamsController.DeleteTeam endpoint.
/// UI uses ArchiveTeamCommand (soft delete) instead.
/// See docs/cleanup/phase3-handler-usage-report.md section 3.2
/// </summary>
/// <param name="Id">Team ID to delete</param>
public sealed record DeleteTeamCommand(int Id) : ICommand<bool>;
