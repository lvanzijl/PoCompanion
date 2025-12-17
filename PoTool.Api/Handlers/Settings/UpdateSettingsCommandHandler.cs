using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for updating application settings.
/// </summary>
public class UpdateSettingsCommandHandler : ICommandHandler<UpdateSettingsCommand, SettingsDto>
{
    private readonly ISettingsRepository _repository;

    public UpdateSettingsCommandHandler(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<SettingsDto> Handle(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        return await _repository.SaveSettingsAsync(
            command.DataMode,
            command.ConfiguredGoalIds,
            cancellationToken);
    }
}
