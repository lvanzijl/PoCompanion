using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for UpdateEffortEstimationSettingsCommand.
/// Updates or creates effort estimation settings.
/// </summary>
public sealed class UpdateEffortEstimationSettingsCommandHandler 
    : ICommandHandler<UpdateEffortEstimationSettingsCommand>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<UpdateEffortEstimationSettingsCommandHandler> _logger;

    public UpdateEffortEstimationSettingsCommandHandler(
        PoToolDbContext dbContext,
        ILogger<UpdateEffortEstimationSettingsCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<Unit> Handle(
        UpdateEffortEstimationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating effort estimation settings");

        var entity = await _dbContext.EffortEstimationSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            entity = new EffortEstimationSettingsEntity();
            _dbContext.EffortEstimationSettings.Add(entity);
        }

        entity.DefaultEffortTask = command.Settings.DefaultEffortTask;
        entity.DefaultEffortBug = command.Settings.DefaultEffortBug;
        entity.DefaultEffortUserStory = command.Settings.DefaultEffortUserStory;
        entity.DefaultEffortPBI = command.Settings.DefaultEffortPBI;
        entity.DefaultEffortFeature = command.Settings.DefaultEffortFeature;
        entity.DefaultEffortEpic = command.Settings.DefaultEffortEpic;
        entity.DefaultEffortGeneric = command.Settings.DefaultEffortGeneric;
        entity.EnableProactiveNotifications = command.Settings.EnableProactiveNotifications;
        entity.LastModified = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Effort estimation settings updated successfully");

        return Unit.Value;
    }
}
