using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Teams;

/// <summary>
/// Handler for creating a new team.
/// </summary>
public class CreateTeamCommandHandler : ICommandHandler<CreateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _repository;

    public CreateTeamCommandHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<TeamDto> Handle(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        return await _repository.CreateTeamAsync(
            command.Name,
            command.TeamAreaPath,
            command.PictureType,
            command.DefaultPictureId,
            command.CustomPicturePath,
            command.ProjectName,
            command.TfsTeamId,
            command.TfsTeamName,
            cancellationToken);
    }
}
