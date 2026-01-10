using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for GetEffortEstimationSettingsQuery.
/// Returns effort estimation settings or defaults if not configured.
/// </summary>
public sealed class GetEffortEstimationSettingsQueryHandler
    : IQueryHandler<GetEffortEstimationSettingsQuery, EffortEstimationSettingsDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<GetEffortEstimationSettingsQueryHandler> _logger;

    public GetEffortEstimationSettingsQueryHandler(
        PoToolDbContext dbContext,
        ILogger<GetEffortEstimationSettingsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<EffortEstimationSettingsDto> Handle(
        GetEffortEstimationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEffortEstimationSettingsQuery");

        var entity = await _dbContext.EffortEstimationSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            _logger.LogInformation("No effort estimation settings found, returning defaults");
            return EffortEstimationSettingsDto.Default;
        }

        return new EffortEstimationSettingsDto(
            entity.DefaultEffortTask,
            entity.DefaultEffortBug,
            entity.DefaultEffortUserStory,
            entity.DefaultEffortPBI,
            entity.DefaultEffortFeature,
            entity.DefaultEffortEpic,
            entity.DefaultEffortGeneric,
            entity.EnableProactiveNotifications
        );
    }
}
