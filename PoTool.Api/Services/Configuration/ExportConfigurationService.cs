using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;
using PoTool.Shared.BugTriage;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.Configuration;

public sealed class ExportConfigurationService
{
    public const string SupportedVersion = "1.0";

    private readonly PoToolDbContext _dbContext;
    private readonly IProfileRepository _profileRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ISettingsRepository _settingsRepository;

    public ExportConfigurationService(
        PoToolDbContext dbContext,
        IProfileRepository profileRepository,
        IProductRepository productRepository,
        ITeamRepository teamRepository,
        ISettingsRepository settingsRepository)
    {
        _dbContext = dbContext;
        _profileRepository = profileRepository;
        _productRepository = productRepository;
        _teamRepository = teamRepository;
        _settingsRepository = settingsRepository;
    }

    public async Task<ConfigurationExportDto> ExportAsync(CancellationToken cancellationToken = default)
    {
        var tfsConfiguration = await _dbContext.TfsConfigs
            .AsNoTracking()
            .OrderByDescending(config => config.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
        var profiles = (await _profileRepository.GetAllProfilesAsync(cancellationToken)).ToList();
        var teams = (await _teamRepository.GetAllTeamsAsync(includeArchived: true, cancellationToken)).ToList();
        var products = (await _productRepository.GetAllProductsAsync(cancellationToken)).ToList();

        var effortSettings = await _dbContext.EffortEstimationSettings
            .AsNoTracking()
            .OrderByDescending(settingsEntity => settingsEntity.LastModified)
            .Select(settingsEntity => new EffortEstimationSettingsDto(
                settingsEntity.DefaultEffortTask,
                settingsEntity.DefaultEffortBug,
                settingsEntity.DefaultEffortUserStory,
                settingsEntity.DefaultEffortPBI,
                settingsEntity.DefaultEffortFeature,
                settingsEntity.DefaultEffortEpic,
                settingsEntity.DefaultEffortGeneric,
                settingsEntity.EnableProactiveNotifications))
            .FirstOrDefaultAsync(cancellationToken);

        var stateClassifications = await _dbContext.WorkItemStateClassifications
            .AsNoTracking()
            .OrderBy(classification => classification.TfsProjectName)
            .ThenBy(classification => classification.WorkItemType)
            .ThenBy(classification => classification.StateName)
            .Select(classification => new ConfigurationStateClassificationDto(
                classification.TfsProjectName,
                classification.WorkItemType,
                classification.StateName,
                (StateClassification)classification.Classification))
            .ToListAsync(cancellationToken);

        var triageTags = await _dbContext.TriageTags
            .AsNoTracking()
            .OrderBy(tag => tag.DisplayOrder)
            .ThenBy(tag => tag.Name)
            .Select(tag => new TriageTagDto(
                tag.Id,
                tag.Name,
                tag.IsEnabled,
                tag.DisplayOrder,
                tag.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ConfigurationExportDto(
            SupportedVersion,
            DateTimeOffset.UtcNow,
            tfsConfiguration,
            settings,
            effortSettings,
            stateClassifications,
            triageTags,
            profiles,
            teams,
            products);
    }
}
