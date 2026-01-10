using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Teams;

/// <summary>
/// Handler for updating an existing team.
/// </summary>
public class UpdateTeamCommandHandler : ICommandHandler<UpdateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _repository;

    public UpdateTeamCommandHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<TeamDto> Handle(UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        return await _repository.UpdateTeamAsync(
            command.Id,
            command.Name,
            command.TeamAreaPath,
            command.PictureType,
            command.DefaultPictureId,
            command.CustomPicturePath,
            cancellationToken);
    }
}
