using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for setting the active profile.
/// </summary>
public class SetActiveProfileCommandHandler : ICommandHandler<SetActiveProfileCommand, SettingsDto>
{
    private readonly ISettingsRepository _repository;

    public SetActiveProfileCommandHandler(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<SettingsDto> Handle(SetActiveProfileCommand command, CancellationToken cancellationToken)
    {
        return await _repository.SetActiveProfileAsync(command.ProfileId, cancellationToken);
    }
}
