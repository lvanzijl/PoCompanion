using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.BugTriage;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.Configuration;

public sealed class ImportConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PoToolDbContext _dbContext;
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<ImportConfigurationService> _logger;

    public ImportConfigurationService(
        PoToolDbContext dbContext,
        ITfsClient tfsClient,
        ILogger<ImportConfigurationService> logger)
    {
        _dbContext = dbContext;
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async Task<ConfigurationImportResultDto> ImportAsync(
        string jsonContent,
        bool validateOnly,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(jsonContent, cancellationToken);

        if (validateOnly)
        {
            await RestoreTfsConfigurationAsync(validation.BackupConfiguration, cancellationToken);
            return validation.ToResult(importExecuted: false);
        }

        if (!validation.CanImport)
        {
            await RestoreTfsConfigurationAsync(validation.BackupConfiguration, cancellationToken);
            return validation.ToResult(importExecuted: false);
        }

        var importedProfiles = new List<string>();
        var warnings = new List<string>(validation.Warnings);
        var errors = new List<string>(validation.Errors);
        var profileIdMap = new Dictionary<int, int>();
        var teamIdMap = new Dictionary<int, int>();

        foreach (var profile in validation.Configuration.Profiles.OrderBy(profile => profile.Name))
        {
            if (!validation.ImportableProfileIds.Contains(profile.Id))
            {
                continue;
            }

            try
            {
                var entity = new ProfileEntity
                {
                    Name = profile.Name,
                    GoalIds = string.Join(",", profile.GoalIds.Distinct()),
                    PictureType = (int)profile.PictureType,
                    DefaultPictureId = profile.DefaultPictureId,
                    CustomPicturePath = profile.CustomPicturePath,
                    CreatedAt = profile.CreatedAt,
                    LastModified = profile.LastModified
                };

                _dbContext.Profiles.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                profileIdMap[profile.Id] = entity.Id;
                importedProfiles.Add(profile.Name);

                _logger.LogInformation("Imported profile {ProfileName} from configuration file", profile.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"Profile '{profile.Name}' could not be imported.");
                _logger.LogError(ex, "Failed to import profile {ProfileName}", profile.Name);
            }
        }

        foreach (var team in validation.Configuration.Teams.OrderBy(team => team.Name))
        {
            if (!validation.ImportableTeamIds.Contains(team.Id))
            {
                continue;
            }

            var existingTeam = await FindExistingTeamAsync(team, cancellationToken);
            if (existingTeam != null)
            {
                teamIdMap[team.Id] = existingTeam.Id;
                warnings.Add($"Team '{team.Name}' already existed and was reused.");
                continue;
            }

            try
            {
                var entity = new TeamEntity
                {
                    Name = team.Name,
                    TeamAreaPath = team.TeamAreaPath,
                    IsArchived = team.IsArchived,
                    PictureType = (int)team.PictureType,
                    DefaultPictureId = team.DefaultPictureId,
                    CustomPicturePath = team.CustomPicturePath,
                    CreatedAt = team.CreatedAt,
                    LastModified = team.LastModified,
                    ProjectName = team.ProjectName,
                    TfsTeamId = team.TfsTeamId,
                    TfsTeamName = team.TfsTeamName,
                    LastSyncedIterationsUtc = team.LastSyncedIterationsUtc
                };

                _dbContext.Teams.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                teamIdMap[team.Id] = entity.Id;

                _logger.LogInformation("Imported team {TeamName} from configuration file", team.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"Team '{team.Name}' could not be imported.");
                _logger.LogError(ex, "Failed to import team {TeamName}", team.Name);
            }
        }

        foreach (var product in validation.Configuration.Products
                     .OrderBy(product => product.ProductOwnerId ?? int.MaxValue)
                     .ThenBy(product => product.Order)
                     .ThenBy(product => product.Name))
        {
            int? importedOwnerId = null;
            if (product.ProductOwnerId.HasValue)
            {
                if (!profileIdMap.TryGetValue(product.ProductOwnerId.Value, out var mappedOwnerId))
                {
                    errors.Add($"Product '{product.Name}' was skipped because its profile was not imported.");
                    continue;
                }

                importedOwnerId = mappedOwnerId;
            }

            if (!validation.ValidBacklogRootIdsByProduct.TryGetValue(product.Id, out var validBacklogRoots)
                || validBacklogRoots.Count == 0)
            {
                errors.Add($"Product '{product.Name}' was skipped because no valid backlog root work items were available.");
                continue;
            }

            try
            {
                var entity = new ProductEntity
                {
                    ProductOwnerId = product.ProductOwnerId.HasValue ? importedOwnerId : null,
                    Name = product.Name,
                    Order = product.Order,
                    PictureType = (int)product.PictureType,
                    DefaultPictureId = product.DefaultPictureId,
                    CustomPicturePath = product.CustomPicturePath,
                    CreatedAt = product.CreatedAt,
                    LastModified = product.LastModified,
                    LastSyncedAt = product.LastSyncedAt
                };

                _dbContext.Products.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);

                foreach (var backlogRootId in validBacklogRoots)
                {
                    _dbContext.ProductBacklogRoots.Add(new ProductBacklogRootEntity
                    {
                        ProductId = entity.Id,
                        WorkItemTfsId = backlogRootId
                    });
                }

                if (!validation.ValidTeamIdsByProduct.TryGetValue(product.Id, out var validTeamIds))
                {
                    validTeamIds = [];
                }

                foreach (var teamId in validTeamIds)
                {
                    if (!teamIdMap.TryGetValue(teamId, out var importedTeamId))
                    {
                        warnings.Add($"Team link for product '{product.Name}' was skipped because the team could not be imported.");
                        continue;
                    }

                    _dbContext.ProductTeamLinks.Add(new ProductTeamLinkEntity
                    {
                        ProductId = entity.Id,
                        TeamId = importedTeamId
                    });
                }

                if (!validation.ValidRepositoryNamesByProduct.TryGetValue(product.Id, out var validRepositoryNames))
                {
                    validRepositoryNames = [];
                }

                foreach (var repositoryName in validRepositoryNames)
                {
                    _dbContext.Repositories.Add(new RepositoryEntity
                    {
                        ProductId = entity.Id,
                        Name = repositoryName,
                        CreatedAt = product.CreatedAt
                    });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Imported product {ProductName} from configuration file", product.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"Product '{product.Name}' could not be imported.");
                _logger.LogError(ex, "Failed to import product {ProductName}", product.Name);
            }
        }

        await ApplyApplicationSettingsAsync(validation.Configuration, profileIdMap, warnings, cancellationToken);
        await ApplyEffortSettingsAsync(validation.Configuration, cancellationToken);
        await ApplyStateClassificationsAsync(validation.Configuration, cancellationToken);
        await ApplyTriageTagsAsync(validation.Configuration, cancellationToken);

        return new ConfigurationImportResultDto(
            CanImport: true,
            ImportExecuted: true,
            ProfilesValidated: validation.ValidatedProfiles,
            ProfilesImported: importedProfiles,
            Warnings: warnings,
            Errors: errors);
    }

    private async Task<ValidationContext> ValidateAsync(string jsonContent, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var validatedProfiles = new List<string>();
        var importableProfileIds = new HashSet<int>();
        var importableTeamIds = new HashSet<int>();
        var validBacklogRootIdsByProduct = new Dictionary<int, List<int>>();
        var validRepositoryNamesByProduct = new Dictionary<int, List<string>>();
        var validTeamIdsByProduct = new Dictionary<int, List<int>>();

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            errors.Add("The selected file did not contain any configuration data.");
            return ValidationContext.Invalid(errors, warnings);
        }

        ConfigurationExportDto? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize<ConfigurationExportDto>(jsonContent, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Configuration import failed because the JSON could not be parsed");
            errors.Add("The selected file is not a valid configuration export.");
            return ValidationContext.Invalid(errors, warnings);
        }

        if (configuration == null)
        {
            errors.Add("The selected file is not a valid configuration export.");
            return ValidationContext.Invalid(errors, warnings);
        }

        if (!string.Equals(configuration.Version, ExportConfigurationService.SupportedVersion, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported configuration version '{configuration.Version}'.");
            return ValidationContext.Invalid(errors, warnings, configuration);
        }

        if (configuration.TfsConfiguration == null
            || string.IsNullOrWhiteSpace(configuration.TfsConfiguration.Url)
            || string.IsNullOrWhiteSpace(configuration.TfsConfiguration.Project))
        {
            errors.Add("The import file does not contain a complete TFS configuration.");
            return ValidationContext.Invalid(errors, warnings, configuration);
        }

        ValidateProfileSchema(configuration, validatedProfiles, importableProfileIds, errors);
        await ValidateProductSchema(configuration, warnings, errors);
        ValidateTeamSchema(configuration, importableTeamIds, errors);

        var backupConfiguration = await ApplyImportedTfsConfigurationAsync(configuration.TfsConfiguration, cancellationToken);

        try
        {
            _logger.LogInformation(
                "Validating imported TFS configuration for Url={Url}, Project={Project}",
                configuration.TfsConfiguration.Url,
                configuration.TfsConfiguration.Project);

            if (!await _tfsClient.ValidateConnectionAsync(cancellationToken))
            {
                errors.Add("TFS connection validation failed.");
                return ValidationContext.Invalid(errors, warnings, configuration, backupConfiguration);
            }

            var projects = (await _tfsClient.GetTfsProjectsAsync(configuration.TfsConfiguration.Url, cancellationToken)).ToList();
            if (!projects.Any(project => string.Equals(project.Name, configuration.TfsConfiguration.Project, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Project '{configuration.TfsConfiguration.Project}' was not found in TFS.");
                return ValidationContext.Invalid(errors, warnings, configuration, backupConfiguration);
            }

            var availableTeams = (await _tfsClient.GetTfsTeamsAsync(cancellationToken)).ToList();
            var availableRepositories = (await _tfsClient.GetGitRepositoriesAsync(cancellationToken))
                .Select(repository => repository.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var workItemTypes = (await _tfsClient.GetWorkItemTypeDefinitionsAsync(cancellationToken))
                .Select(definition => definition.TypeName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var team in configuration.Teams)
            {
                if (!importableTeamIds.Contains(team.Id))
                {
                    continue;
                }

                var teamExists = availableTeams.Any(availableTeam =>
                    (!string.IsNullOrWhiteSpace(team.TfsTeamId)
                     && string.Equals(availableTeam.Id, team.TfsTeamId, StringComparison.OrdinalIgnoreCase))
                    || string.Equals(availableTeam.Name, team.TfsTeamName ?? team.Name, StringComparison.OrdinalIgnoreCase));

                if (!teamExists)
                {
                    warnings.Add($"Team '{team.Name}' was not found in TFS and its links will be skipped.");
                    importableTeamIds.Remove(team.Id);
                }
            }

            foreach (var product in configuration.Products)
            {
                var validBacklogRoots = new List<int>();
                foreach (var backlogRootId in product.BacklogRootWorkItemIds.Distinct())
                {
                    _logger.LogInformation(
                        "Validating backlog root work item {BacklogRootId} for product {ProductName}",
                        backlogRootId,
                        product.Name);

                    var workItem = await _tfsClient.GetWorkItemByIdAsync(backlogRootId, cancellationToken);
                    if (workItem == null)
                    {
                        errors.Add($"Work item {backlogRootId} for product '{product.Name}' was not found.");
                        continue;
                    }

                    if (!workItemTypes.Contains(workItem.Type))
                    {
                        errors.Add($"Work item {backlogRootId} for product '{product.Name}' uses unknown type '{workItem.Type}'.");
                        continue;
                    }

                    validBacklogRoots.Add(backlogRootId);
                }

                validBacklogRootIdsByProduct[product.Id] = validBacklogRoots;

                var validRepositoryNames = new List<string>();
                foreach (var repository in product.Repositories)
                {
                    if (availableRepositories.Contains(repository.Name))
                    {
                        validRepositoryNames.Add(repository.Name);
                    }
                    else
                    {
                        errors.Add($"Repository '{repository.Name}' for product '{product.Name}' was not accessible.");
                    }
                }

                validRepositoryNamesByProduct[product.Id] = validRepositoryNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                validTeamIdsByProduct[product.Id] = product.TeamIds
                    .Where(importableTeamIds.Contains)
                    .Distinct()
                    .ToList();
            }

            return new ValidationContext(
                configuration,
                backupConfiguration,
                true,
                validatedProfiles,
                importableProfileIds,
                importableTeamIds,
                validBacklogRootIdsByProduct,
                validRepositoryNamesByProduct,
                validTeamIdsByProduct,
                warnings,
                errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration import validation failed unexpectedly");
            errors.Add("TFS validation failed unexpectedly.");
            return ValidationContext.Invalid(errors, warnings, configuration, backupConfiguration);
        }
    }

    private async Task<TeamEntity?> FindExistingTeamAsync(TeamDto team, CancellationToken cancellationToken)
    {
        var existingTeams = await _dbContext.Teams
            .OrderBy(existingTeam => existingTeam.Id)
            .ToListAsync(cancellationToken);

        return existingTeams.FirstOrDefault(existingTeam =>
            (!string.IsNullOrWhiteSpace(team.TfsTeamId)
             && string.Equals(existingTeam.TfsTeamId, team.TfsTeamId, StringComparison.OrdinalIgnoreCase))
            || string.Equals(existingTeam.Name, team.Name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ApplyApplicationSettingsAsync(
        ConfigurationExportDto configuration,
        IReadOnlyDictionary<int, int> profileIdMap,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var settingsEntity = await _dbContext.Settings
            .OrderBy(settings => settings.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settingsEntity == null)
        {
            settingsEntity = new SettingsEntity
            {
                LastModified = DateTimeOffset.UtcNow
            };
            _dbContext.Settings.Add(settingsEntity);
        }

        settingsEntity.ActiveProfileId = null;
        settingsEntity.LastModified = configuration.Settings?.LastModified ?? DateTimeOffset.UtcNow;

        if (configuration.Settings?.ActiveProfileId is int exportedActiveProfileId)
        {
            if (profileIdMap.TryGetValue(exportedActiveProfileId, out var importedActiveProfileId))
            {
                settingsEntity.ActiveProfileId = importedActiveProfileId;
            }
            else
            {
                warnings.Add("The imported active profile could not be restored.");
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyEffortSettingsAsync(ConfigurationExportDto configuration, CancellationToken cancellationToken)
    {
        if (configuration.EffortEstimationSettings == null)
        {
            return;
        }

        var entity = await _dbContext.EffortEstimationSettings
            .OrderByDescending(settings => settings.LastModified)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            entity = new EffortEstimationSettingsEntity();
            _dbContext.EffortEstimationSettings.Add(entity);
        }

        entity.DefaultEffortTask = configuration.EffortEstimationSettings.DefaultEffortTask;
        entity.DefaultEffortBug = configuration.EffortEstimationSettings.DefaultEffortBug;
        entity.DefaultEffortUserStory = configuration.EffortEstimationSettings.DefaultEffortUserStory;
        entity.DefaultEffortPBI = configuration.EffortEstimationSettings.DefaultEffortPBI;
        entity.DefaultEffortFeature = configuration.EffortEstimationSettings.DefaultEffortFeature;
        entity.DefaultEffortEpic = configuration.EffortEstimationSettings.DefaultEffortEpic;
        entity.DefaultEffortGeneric = configuration.EffortEstimationSettings.DefaultEffortGeneric;
        entity.EnableProactiveNotifications = configuration.EffortEstimationSettings.EnableProactiveNotifications;
        entity.LastModified = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyStateClassificationsAsync(ConfigurationExportDto configuration, CancellationToken cancellationToken)
    {
        var projectNames = configuration.StateClassifications
            .Select(classification => classification.ProjectName)
            .Where(projectName => !string.IsNullOrWhiteSpace(projectName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _dbContext.WorkItemStateClassifications
            .Where(classification => projectNames.Contains(classification.TfsProjectName))
            .ToListAsync(cancellationToken);

        _dbContext.WorkItemStateClassifications.RemoveRange(existing);

        foreach (var classification in configuration.StateClassifications)
        {
            _dbContext.WorkItemStateClassifications.Add(new WorkItemStateClassificationEntity
            {
                TfsProjectName = classification.ProjectName,
                WorkItemType = classification.WorkItemType,
                StateName = classification.StateName,
                Classification = (int)classification.Classification,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyTriageTagsAsync(ConfigurationExportDto configuration, CancellationToken cancellationToken)
    {
        var existingTags = await _dbContext.TriageTags.ToListAsync(cancellationToken);
        _dbContext.TriageTags.RemoveRange(existingTags);

        foreach (var tag in configuration.TriageTags.OrderBy(tag => tag.DisplayOrder).ThenBy(tag => tag.Name))
        {
            _dbContext.TriageTags.Add(new TriageTagEntity
            {
                Name = tag.Name,
                IsEnabled = tag.IsEnabled,
                DisplayOrder = tag.DisplayOrder,
                CreatedAt = tag.CreatedAt
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TfsConfigEntity?> ApplyImportedTfsConfigurationAsync(
        TfsConfigEntity importedConfiguration,
        CancellationToken cancellationToken)
    {
        var current = await _dbContext.TfsConfigs
            .OrderByDescending(config => config.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var backup = current == null
            ? null
            : CloneTfsConfiguration(current);

        if (current == null)
        {
            current = new TfsConfigEntity
            {
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.TfsConfigs.Add(current);
        }

        current.Url = importedConfiguration.Url;
        current.Project = importedConfiguration.Project;
        current.DefaultAreaPath = importedConfiguration.DefaultAreaPath;
        current.UseDefaultCredentials = importedConfiguration.UseDefaultCredentials;
        current.TimeoutSeconds = importedConfiguration.TimeoutSeconds;
        current.ApiVersion = importedConfiguration.ApiVersion;
        current.LastValidated = null;
        current.HasTestedConnectionSuccessfully = false;
        current.HasVerifiedTfsApiSuccessfully = false;
        current.UpdatedAt = DateTimeOffset.UtcNow;
        current.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return backup;
    }

    private async Task RestoreTfsConfigurationAsync(TfsConfigEntity? backupConfiguration, CancellationToken cancellationToken)
    {
        var current = await _dbContext.TfsConfigs
            .OrderByDescending(config => config.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (backupConfiguration == null)
        {
            if (current != null)
            {
                _dbContext.TfsConfigs.Remove(current);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (current == null)
        {
            _dbContext.TfsConfigs.Add(backupConfiguration);
        }
        else
        {
            current.Url = backupConfiguration.Url;
            current.Project = backupConfiguration.Project;
            current.DefaultAreaPath = backupConfiguration.DefaultAreaPath;
            current.UseDefaultCredentials = backupConfiguration.UseDefaultCredentials;
            current.TimeoutSeconds = backupConfiguration.TimeoutSeconds;
            current.ApiVersion = backupConfiguration.ApiVersion;
            current.LastValidated = backupConfiguration.LastValidated;
            current.HasTestedConnectionSuccessfully = backupConfiguration.HasTestedConnectionSuccessfully;
            current.HasVerifiedTfsApiSuccessfully = backupConfiguration.HasVerifiedTfsApiSuccessfully;
            current.CreatedAt = backupConfiguration.CreatedAt;
            current.UpdatedAt = backupConfiguration.UpdatedAt;
            current.UpdatedAtUtc = backupConfiguration.UpdatedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TfsConfigEntity CloneTfsConfiguration(TfsConfigEntity source)
    {
        return new TfsConfigEntity
        {
            Id = source.Id,
            Url = source.Url,
            Project = source.Project,
            DefaultAreaPath = source.DefaultAreaPath,
            UseDefaultCredentials = source.UseDefaultCredentials,
            TimeoutSeconds = source.TimeoutSeconds,
            ApiVersion = source.ApiVersion,
            LastValidated = source.LastValidated,
            HasTestedConnectionSuccessfully = source.HasTestedConnectionSuccessfully,
            HasVerifiedTfsApiSuccessfully = source.HasVerifiedTfsApiSuccessfully,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }

    private static void ValidateProfileSchema(
        ConfigurationExportDto configuration,
        ICollection<string> validatedProfiles,
        ISet<int> importableProfileIds,
        ICollection<string> errors)
    {
        var duplicateNames = configuration.Profiles
            .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in configuration.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                errors.Add("A profile in the import file is missing its name.");
                continue;
            }

            if (duplicateNames.Contains(profile.Name))
            {
                errors.Add($"Profile '{profile.Name}' appears multiple times in the import file.");
                continue;
            }

            validatedProfiles.Add(profile.Name);
            importableProfileIds.Add(profile.Id);
        }
    }

    private async Task ValidateProductSchema(
        ConfigurationExportDto configuration,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        var existingProfileNames = await _dbContext.Profiles
            .Select(profile => profile.Name)
            .ToListAsync();
        var existingProfileNameSet = existingProfileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in configuration.Profiles)
        {
            if (existingProfileNameSet.Contains(profile.Name))
            {
                errors.Add($"Profile '{profile.Name}' already exists.");
            }
        }

        var availableProfileIds = configuration.Profiles.Select(profile => profile.Id).ToHashSet();
        var availableTeamIds = configuration.Teams.Select(team => team.Id).ToHashSet();

        foreach (var product in configuration.Products)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
            {
                errors.Add("A product in the import file is missing its name.");
            }

            if (product.ProductOwnerId.HasValue && !availableProfileIds.Contains(product.ProductOwnerId.Value))
            {
                errors.Add($"Product '{product.Name}' references an unknown profile.");
            }

            if (product.BacklogRootWorkItemIds.Count == 0)
            {
                errors.Add($"Product '{product.Name}' does not contain any backlog root work item IDs.");
            }

            foreach (var teamId in product.TeamIds)
            {
                if (!availableTeamIds.Contains(teamId))
                {
                    warnings.Add($"Product '{product.Name}' references an unknown team and the link will be skipped.");
                }
            }

            foreach (var repository in product.Repositories)
            {
                if (string.IsNullOrWhiteSpace(repository.Name))
                {
                    errors.Add($"Product '{product.Name}' contains a repository entry without a name.");
                }
            }
        }
    }

    private static void ValidateTeamSchema(
        ConfigurationExportDto configuration,
        ISet<int> importableTeamIds,
        ICollection<string> errors)
    {
        var duplicateNames = configuration.Teams
            .GroupBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var team in configuration.Teams)
        {
            if (string.IsNullOrWhiteSpace(team.Name))
            {
                errors.Add("A team in the import file is missing its name.");
                continue;
            }

            if (duplicateNames.Contains(team.Name))
            {
                errors.Add($"Team '{team.Name}' appears multiple times in the import file.");
                continue;
            }

            importableTeamIds.Add(team.Id);
        }
    }

    private sealed record ValidationContext(
        ConfigurationExportDto Configuration,
        TfsConfigEntity? BackupConfiguration,
        bool CanImport,
        IReadOnlyList<string> ValidatedProfiles,
        ISet<int> ImportableProfileIds,
        ISet<int> ImportableTeamIds,
        IReadOnlyDictionary<int, List<int>> ValidBacklogRootIdsByProduct,
        IReadOnlyDictionary<int, List<string>> ValidRepositoryNamesByProduct,
        IReadOnlyDictionary<int, List<int>> ValidTeamIdsByProduct,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Errors)
    {
        public static ValidationContext Invalid(
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            ConfigurationExportDto? configuration = null,
            TfsConfigEntity? backupConfiguration = null)
        {
            return new ValidationContext(
                configuration ?? new ConfigurationExportDto(
                    ExportConfigurationService.SupportedVersion,
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    null,
                    [],
                    [],
                    [],
                    [],
                    []),
                backupConfiguration,
                false,
                [],
                new HashSet<int>(),
                new HashSet<int>(),
                new Dictionary<int, List<int>>(),
                new Dictionary<int, List<string>>(),
                new Dictionary<int, List<int>>(),
                warnings,
                errors);
        }

        public ConfigurationImportResultDto ToResult(bool importExecuted)
        {
            return new ConfigurationImportResultDto(
                CanImport,
                importExecuted,
                ValidatedProfiles.ToList(),
                [],
                Warnings.ToList(),
                Errors.ToList());
        }
    }
}
