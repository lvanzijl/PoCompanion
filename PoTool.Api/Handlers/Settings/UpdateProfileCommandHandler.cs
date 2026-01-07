using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for updating an existing profile.
/// </summary>
public class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand, ProfileDto>
{
    private readonly IProfileRepository _repository;

    public UpdateProfileCommandHandler(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<ProfileDto> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        return await _repository.UpdateProfileAsync(
            command.Id,
            command.Name,
            command.AreaPaths,
            command.TeamName,
            command.GoalIds,
            cancellationToken: cancellationToken);
    }
}
