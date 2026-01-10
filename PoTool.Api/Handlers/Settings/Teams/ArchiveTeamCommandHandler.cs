using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Teams;

/// <summary>
/// Handler for archiving or unarchiving a team.
/// </summary>
public class ArchiveTeamCommandHandler : ICommandHandler<ArchiveTeamCommand, TeamDto>
{
    private readonly ITeamRepository _repository;

    public ArchiveTeamCommandHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<TeamDto> Handle(ArchiveTeamCommand command, CancellationToken cancellationToken)
    {
        return await _repository.ArchiveTeamAsync(command.Id, command.IsArchived, cancellationToken);
    }
}
