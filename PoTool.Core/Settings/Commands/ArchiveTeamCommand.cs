using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to archive or unarchive a team.
/// Archived teams remain available for historical classification but are hidden from selection controls.
/// </summary>
/// <param name="Id">Team ID</param>
/// <param name="IsArchived">True to archive, false to unarchive</param>
public sealed record ArchiveTeamCommand(
    int Id,
    bool IsArchived
) : ICommand<TeamDto>;
