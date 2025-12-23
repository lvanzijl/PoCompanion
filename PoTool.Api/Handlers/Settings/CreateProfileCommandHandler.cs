using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for creating a new profile.
/// </summary>
public class CreateProfileCommandHandler : ICommandHandler<CreateProfileCommand, ProfileDto>
{
    private readonly IProfileRepository _repository;

    public CreateProfileCommandHandler(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<ProfileDto> Handle(CreateProfileCommand command, CancellationToken cancellationToken)
    {
        return await _repository.CreateProfileAsync(
            command.Name,
            command.AreaPaths,
            command.TeamName,
            command.GoalIds,
            cancellationToken);
    }
}
