using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<ExportConfigurationService> _logger;

    public ExportConfigurationService(
        PoToolDbContext dbContext,
        IProfileRepository profileRepository,
        IProductRepository productRepository,
        ITeamRepository teamRepository,
        ISettingsRepository settingsRepository,
        ITfsClient tfsClient,
        ILogger<ExportConfigurationService> logger)
    {
        _dbContext = dbContext;
        _profileRepository = profileRepository;
        _productRepository = productRepository;
        _teamRepository = teamRepository;
        _settingsRepository = settingsRepository;
        _tfsClient = tfsClient;
        _logger = logger;
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
        products = await EnrichRepositoriesWithIdentifiersAsync(products, tfsConfiguration != null, cancellationToken);

        var effortSettings = await _dbContext.EffortEstimationSettings
            .AsNoTracking()
            .OrderByDescending(settingsEntity => settingsEntity.LastModified.UtcDateTime)
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

    private async Task<List<ProductDto>> EnrichRepositoriesWithIdentifiersAsync(
        List<ProductDto> products,
        bool hasTfsConfiguration,
        CancellationToken cancellationToken)
    {
        if (!hasTfsConfiguration || products.All(product => product.Repositories.Count == 0))
        {
            return products;
        }

        try
        {
            var repositoriesByName = (await _tfsClient.GetGitRepositoriesAsync(cancellationToken))
                .Where(repository => !string.IsNullOrWhiteSpace(repository.Name))
                .GroupBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var repositoryIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var repositoryGroup in repositoriesByName)
            {
                if (repositoryGroup.Count() != 1)
                {
                    _logger.LogWarning(
                        "Skipping repository identifier export for duplicate repository name '{RepositoryName}'",
                        repositoryGroup.Key);
                    continue;
                }

                var repository = repositoryGroup.Single();
                repositoryIdsByName[repository.Name] = repository.Id;
            }

            return products
                .Select(product => product with
                {
                    Repositories = product.Repositories
                        .Select(repository => repositoryIdsByName.TryGetValue(repository.Name, out var repositoryId)
                            ? repository with { RepositoryId = repositoryId }
                            : repository)
                        .ToList()
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve repository identifiers during configuration export");
            return products;
        }
    }
}
