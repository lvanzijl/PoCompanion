using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;
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

    private const string ProfileEntityType = "Profile";
    private const string ProductEntityType = "Product";
    private const string TeamEntityType = "Team";
    private const string RepositoryEntityType = "Repository";
    private const string GlobalSettingsEntityType = "GlobalSettings";

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
        bool wipeExistingConfiguration = false,
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

        if (validation.ExistingConfigurationDetected && !wipeExistingConfiguration)
        {
            await RestoreTfsConfigurationAsync(validation.BackupConfiguration, cancellationToken);
            return validation.ToResult(importExecuted: false, requiresDestructiveConfirmation: true);
        }

        var removedItems = new List<string>();
        var trustedConfigurationId = validation.ImportedConfigurationId;
        if (validation.ExistingConfigurationDetected)
        {
            await RestoreTfsConfigurationAsync(validation.BackupConfiguration, cancellationToken);
            removedItems = await WipeExistingConfigurationAsync(cancellationToken);
            var appliedConfiguration = await ApplyImportedTfsConfigurationAsync(validation.Configuration.TfsConfiguration!, cancellationToken);
            trustedConfigurationId = appliedConfiguration.ConfigurationId;
        }

        var importedProfiles = new List<string>();
        var warnings = new List<string>(validation.Warnings);
        var errors = new List<string>(validation.Errors);
        var structuredProfilesImported = new List<ConfigurationImportEntityResultDto>(validation.StructuredProfilesImported);
        var productsImported = new List<ConfigurationImportEntityResultDto>(validation.ProductsImported);
        var teamsImported = new List<ConfigurationImportEntityResultDto>(validation.TeamsImported);
        var repositoriesLinked = new List<ConfigurationImportEntityResultDto>(validation.RepositoriesLinked);
        var globalSettingsApplied = new List<ConfigurationImportEntityResultDto>(validation.GlobalSettingsApplied);
        var profileIdMap = new Dictionary<int, int>();
        var teamIdMap = new Dictionary<int, int>();
        var importedProject = await EnsureImportedProjectAsync(validation.Configuration.TfsConfiguration?.Project, cancellationToken);

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
                structuredProfilesImported.Add(CreateEntityResult(ProfileEntityType, profile.Name, ConfigurationImportEntityStatus.Success));

                _logger.LogInformation("Imported profile {ProfileName} from configuration file", profile.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"Profile '{profile.Name}' could not be imported.");
                structuredProfilesImported.Add(CreateEntityResult(
                    ProfileEntityType,
                    DescribeName(profile.Name, "Unnamed profile"),
                    ConfigurationImportEntityStatus.Error,
                    "The profile could not be imported."));
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
                teamsImported.Add(CreateEntityResult(
                    TeamEntityType,
                    team.Name,
                    ConfigurationImportEntityStatus.Warning,
                    "The team already existed and was reused."));
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
                teamsImported.Add(CreateEntityResult(TeamEntityType, team.Name, ConfigurationImportEntityStatus.Success));

                _logger.LogInformation("Imported team {TeamName} from configuration file", team.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"Team '{team.Name}' could not be imported.");
                teamsImported.Add(CreateEntityResult(
                    TeamEntityType,
                    DescribeName(team.Name, "Unnamed team"),
                    ConfigurationImportEntityStatus.Error,
                    "The team could not be imported."));
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
                    productsImported.Add(CreateEntityResult(
                        ProductEntityType,
                        DescribeName(product.Name, "Unnamed product"),
                        ConfigurationImportEntityStatus.Skipped,
                        "Product owner profile was not imported."));
                    continue;
                }

                importedOwnerId = mappedOwnerId;
            }

            if (!validation.ValidBacklogRootIdsByProduct.TryGetValue(product.Id, out var validBacklogRoots)
                || validBacklogRoots.Count == 0)
            {
                errors.Add($"Product '{product.Name}' was skipped because no valid backlog root work items were available.");
                productsImported.Add(CreateEntityResult(
                    ProductEntityType,
                    DescribeName(product.Name, "Unnamed product"),
                    ConfigurationImportEntityStatus.Skipped,
                    "No valid backlog root work items were available."));
                continue;
            }

            try
            {
                var entity = new ProductEntity
                {
                    ProductOwnerId = product.ProductOwnerId.HasValue ? importedOwnerId : null,
                    Name = product.Name,
                    ProjectId = importedProject.Id,
                    Order = product.Order,
                    PictureType = (int)product.PictureType,
                    DefaultPictureId = product.DefaultPictureId,
                    CustomPicturePath = product.CustomPicturePath,
                    EstimationMode = (int)product.EstimationMode,
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
                    repositoriesLinked.Add(CreateEntityResult(
                        RepositoryEntityType,
                        DescribeRepositoryName(repositoryName, product.Name),
                        ConfigurationImportEntityStatus.Success,
                        $"Linked to product '{product.Name}'."));
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                productsImported.Add(CreateEntityResult(ProductEntityType, product.Name, ConfigurationImportEntityStatus.Success));
                _logger.LogInformation("Imported product {ProductName} from configuration file", product.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"Product '{product.Name}' could not be imported.");
                productsImported.Add(CreateEntityResult(
                    ProductEntityType,
                    DescribeName(product.Name, "Unnamed product"),
                    ConfigurationImportEntityStatus.Error,
                    "The product could not be imported."));
                _logger.LogError(ex, "Failed to import product {ProductName}", product.Name);
            }
        }

        globalSettingsApplied.Add(await ApplyApplicationSettingsAsync(validation.Configuration, profileIdMap, warnings, cancellationToken));
        globalSettingsApplied.Add(await ApplyEffortSettingsAsync(validation.Configuration, cancellationToken));
        globalSettingsApplied.Add(await ApplyStateClassificationsAsync(validation.Configuration, cancellationToken));
        globalSettingsApplied.Add(await ApplyTriageTagsAsync(validation.Configuration, cancellationToken));
        await MarkImportedTfsConfigurationAsTrustedReadyAsync(trustedConfigurationId, cancellationToken);

        return new ConfigurationImportResultDto(
            CanImport: true,
            ImportExecuted: true,
            ExistingConfigurationDetected: validation.ExistingConfigurationDetected,
            RequiresDestructiveConfirmation: false,
            ProfilesValidated: validation.ValidatedProfiles,
            ProfilesImported: importedProfiles,
            ExistingConfigurationSummary: validation.ExistingConfigurationSummary,
            RemovedItems: removedItems,
            Warnings: warnings,
            Errors: errors,
            StructuredProfilesImported: structuredProfilesImported,
            ProductsImported: productsImported,
            TeamsImported: teamsImported,
            RepositoriesLinked: repositoriesLinked,
            GlobalSettingsApplied: globalSettingsApplied);
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
        var existingConfigurationSummary = await GetExistingConfigurationSummaryAsync(cancellationToken);
        var structuredProfilesImported = new List<ConfigurationImportEntityResultDto>();
        var productsImported = new List<ConfigurationImportEntityResultDto>();
        var teamsImported = new List<ConfigurationImportEntityResultDto>();
        var repositoriesLinked = new List<ConfigurationImportEntityResultDto>();
        var globalSettingsApplied = new List<ConfigurationImportEntityResultDto>();

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

        ValidateProfileSchema(configuration, validatedProfiles, importableProfileIds, errors, structuredProfilesImported);
        ValidateProductSchema(configuration, warnings, errors, repositoriesLinked);
        ValidateTeamSchema(configuration, importableTeamIds, errors, teamsImported);

        var appliedConfiguration = await ApplyImportedTfsConfigurationAsync(configuration.TfsConfiguration, cancellationToken);
        var backupConfiguration = appliedConfiguration.BackupConfiguration;

        try
        {
            _logger.LogInformation(
                "Validating imported TFS configuration for Url={Url}, Project={Project}",
                configuration.TfsConfiguration.Url,
                configuration.TfsConfiguration.Project);

            if (!await _tfsClient.ValidateConnectionAsync(cancellationToken))
            {
                errors.Add("TFS connection validation failed.");
                return ValidationContext.Invalid(
                    errors,
                    warnings,
                    configuration,
                    backupConfiguration,
                    structuredProfilesImported,
                    productsImported,
                    teamsImported,
                    repositoriesLinked,
                    globalSettingsApplied);
            }

            var projects = (await _tfsClient.GetTfsProjectsAsync(configuration.TfsConfiguration.Url, cancellationToken)).ToList();
            if (!projects.Any(project => string.Equals(project.Name, configuration.TfsConfiguration.Project, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Project '{configuration.TfsConfiguration.Project}' was not found in TFS.");
                return ValidationContext.Invalid(
                    errors,
                    warnings,
                    configuration,
                    backupConfiguration,
                    structuredProfilesImported,
                    productsImported,
                    teamsImported,
                    repositoriesLinked,
                    globalSettingsApplied);
            }

            var availableTeams = (await _tfsClient.GetTfsTeamsAsync(cancellationToken)).ToList();
            var availableRepositories = (await _tfsClient.GetGitRepositoriesAsync(cancellationToken))
                .Where(repository => !string.IsNullOrWhiteSpace(repository.Name))
                .ToList();
            var availableRepositoriesById = availableRepositories
                .Where(repository => !string.IsNullOrWhiteSpace(repository.Id))
                .ToDictionary(
                    repository => repository.Id,
                    repository => repository,
                    StringComparer.OrdinalIgnoreCase);
            var availableRepositoryGroupsByName = availableRepositories
                .GroupBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
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
                    teamsImported.Add(CreateEntityResult(
                        TeamEntityType,
                        team.Name,
                        ConfigurationImportEntityStatus.Warning,
                        "The team was not found in TFS and its links will be skipped."));
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
                    if (!string.IsNullOrWhiteSpace(repository.RepositoryId)
                        && availableRepositoriesById.TryGetValue(repository.RepositoryId, out var repositoryById))
                    {
                        validRepositoryNames.Add(repositoryById.Name);
                        continue;
                    }

                    if (availableRepositoryGroupsByName.TryGetValue(repository.Name, out var repositoriesByName))
                    {
                        validRepositoryNames.Add(repository.Name);

                        if (repositoriesByName.Count > 1)
                        {
                            var warningMessage =
                                $"Repository '{repository.Name}' for product '{product.Name}' matched multiple repositories by name and could not be uniquely identified.";
                            warnings.Add(warningMessage);
                            repositoriesLinked.Add(CreateEntityResult(
                                RepositoryEntityType,
                                DescribeRepositoryName(repository.Name, product.Name),
                                ConfigurationImportEntityStatus.Warning,
                                warningMessage));
                        }
                        else if (!string.IsNullOrWhiteSpace(repository.RepositoryId))
                        {
                            var warningMessage =
                                $"Repository '{repository.Name}' for product '{product.Name}' was found by name, but repository ID '{repository.RepositoryId}' was not accessible.";
                            warnings.Add(warningMessage);
                            repositoriesLinked.Add(CreateEntityResult(
                                RepositoryEntityType,
                                DescribeRepositoryName(repository.Name, product.Name),
                                ConfigurationImportEntityStatus.Warning,
                                warningMessage));
                        }

                        continue;
                    }

                    var errorMessage = $"Repository '{repository.Name}' for product '{product.Name}' was not found.";
                    errors.Add(errorMessage);
                    repositoriesLinked.Add(CreateEntityResult(
                        RepositoryEntityType,
                        DescribeRepositoryName(repository.Name, product.Name),
                        ConfigurationImportEntityStatus.Error,
                        errorMessage));
                }

                validRepositoryNamesByProduct[product.Id] = validRepositoryNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                validTeamIdsByProduct[product.Id] = product.TeamIds
                    .Where(importableTeamIds.Contains)
                    .Distinct()
                    .ToList();
            }

            return ValidationContext.Create(
                configuration,
                backupConfiguration,
                appliedConfiguration.ConfigurationId,
                true,
                validatedProfiles,
                importableProfileIds,
                importableTeamIds,
                validBacklogRootIdsByProduct,
                validRepositoryNamesByProduct,
                validTeamIdsByProduct,
                existingConfigurationSummary,
                warnings,
                errors,
                structuredProfilesImported,
                productsImported,
                teamsImported,
                repositoriesLinked,
                globalSettingsApplied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration import validation failed unexpectedly");
            errors.Add("TFS validation failed unexpectedly.");
            return ValidationContext.Invalid(
                errors,
                warnings,
                configuration,
                backupConfiguration,
                structuredProfilesImported,
                productsImported,
                teamsImported,
                repositoriesLinked,
                globalSettingsApplied);
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

    private async Task<ConfigurationImportEntityResultDto> ApplyApplicationSettingsAsync(
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
        return CreateEntityResult(
            GlobalSettingsEntityType,
            "Application settings",
            settingsEntity.ActiveProfileId.HasValue || configuration.Settings?.ActiveProfileId is not int
                ? ConfigurationImportEntityStatus.Success
                : ConfigurationImportEntityStatus.Warning,
            settingsEntity.ActiveProfileId.HasValue || configuration.Settings?.ActiveProfileId is not int
                ? "Application settings were applied."
                : "The imported active profile could not be restored.");
    }

    private async Task<ConfigurationImportEntityResultDto> ApplyEffortSettingsAsync(ConfigurationExportDto configuration, CancellationToken cancellationToken)
    {
        if (configuration.EffortEstimationSettings == null)
        {
            return CreateEntityResult(
                GlobalSettingsEntityType,
                "Effort estimation settings",
                ConfigurationImportEntityStatus.Skipped,
                "No effort estimation settings were present in the import file.");
        }

        var entity = await _dbContext.EffortEstimationSettings
            .OrderByDescending(settings => settings.LastModifiedUtc)
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
        return CreateEntityResult(
            GlobalSettingsEntityType,
            "Effort estimation settings",
            ConfigurationImportEntityStatus.Success,
            "Effort estimation settings were applied.");
    }

    private async Task<ConfigurationImportEntityResultDto> ApplyStateClassificationsAsync(ConfigurationExportDto configuration, CancellationToken cancellationToken)
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
        return CreateEntityResult(
            GlobalSettingsEntityType,
            "State classifications",
            ConfigurationImportEntityStatus.Success,
            $"Applied {configuration.StateClassifications.Count} state classification(s).");
    }

    private async Task<ConfigurationImportEntityResultDto> ApplyTriageTagsAsync(ConfigurationExportDto configuration, CancellationToken cancellationToken)
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
        return CreateEntityResult(
            GlobalSettingsEntityType,
            "Triage tags",
            ConfigurationImportEntityStatus.Success,
            $"Applied {configuration.TriageTags.Count} triage tag(s).");
    }

    private async Task<AppliedImportedTfsConfiguration> ApplyImportedTfsConfigurationAsync(
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
        return new AppliedImportedTfsConfiguration(backup, current.Id);
    }

    private async Task MarkImportedTfsConfigurationAsTrustedReadyAsync(int? configurationId, CancellationToken cancellationToken)
    {
        if (!configurationId.HasValue)
        {
            _logger.LogWarning("Skipping trusted-ready marking because the imported TFS configuration ID was not available.");
            return;
        }

        var current = await _dbContext.TfsConfigs
            .FirstOrDefaultAsync(config => config.Id == configurationId.Value, cancellationToken);

        if (current == null)
        {
            _logger.LogWarning(
                "Skipping trusted-ready marking because imported TFS configuration {ConfigurationId} was not found.",
                configurationId.Value);
            return;
        }

        current.HasTestedConnectionSuccessfully = true;
        current.HasVerifiedTfsApiSuccessfully = true;

        await _dbContext.SaveChangesAsync(cancellationToken);
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
            CopyTfsConfiguration(current, backupConfiguration);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TfsConfigEntity CloneTfsConfiguration(TfsConfigEntity source)
    {
        var clone = new TfsConfigEntity();
        CopyTfsConfiguration(clone, source);
        clone.Id = source.Id;
        return clone;
    }

    private static void CopyTfsConfiguration(TfsConfigEntity target, TfsConfigEntity source)
    {
        target.Url = source.Url;
        target.Project = source.Project;
        target.DefaultAreaPath = source.DefaultAreaPath;
        target.UseDefaultCredentials = source.UseDefaultCredentials;
        target.TimeoutSeconds = source.TimeoutSeconds;
        target.ApiVersion = source.ApiVersion;
        target.LastValidated = source.LastValidated;
        target.HasTestedConnectionSuccessfully = source.HasTestedConnectionSuccessfully;
        target.HasVerifiedTfsApiSuccessfully = source.HasVerifiedTfsApiSuccessfully;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
    }

    private static void ValidateProfileSchema(
        ConfigurationExportDto configuration,
        ICollection<string> validatedProfiles,
        ISet<int> importableProfileIds,
        ICollection<string> errors,
        ICollection<ConfigurationImportEntityResultDto> structuredProfilesImported)
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
                structuredProfilesImported.Add(CreateEntityResult(
                    ProfileEntityType,
                    "Unnamed profile",
                    ConfigurationImportEntityStatus.Error,
                    "A profile in the import file is missing its name."));
                continue;
            }

            if (duplicateNames.Contains(profile.Name))
            {
                errors.Add($"Profile '{profile.Name}' appears multiple times in the import file.");
                structuredProfilesImported.Add(CreateEntityResult(
                    ProfileEntityType,
                    profile.Name,
                    ConfigurationImportEntityStatus.Error,
                    "The profile appears multiple times in the import file."));
                continue;
            }

            validatedProfiles.Add(profile.Name);
            importableProfileIds.Add(profile.Id);
        }
    }

    private static void ValidateProductSchema(
        ConfigurationExportDto configuration,
        ICollection<string> warnings,
        ICollection<string> errors,
        ICollection<ConfigurationImportEntityResultDto> repositoriesLinked)
    {
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
                    repositoriesLinked.Add(CreateEntityResult(
                        RepositoryEntityType,
                        DescribeRepositoryName("Unnamed repository", product.Name),
                        ConfigurationImportEntityStatus.Error,
                        $"Product '{product.Name}' contains a repository entry without a name."));
                }
            }
        }
    }

    private async Task<List<string>> GetExistingConfigurationSummaryAsync(CancellationToken cancellationToken)
    {
        var summary = new List<string>();

        var profileCount = await _dbContext.Profiles.CountAsync(cancellationToken);
        if (profileCount > 0)
        {
            summary.Add($"{profileCount} existing profile(s)");
        }

        var teamCount = await _dbContext.Teams.CountAsync(cancellationToken);
        if (teamCount > 0)
        {
            summary.Add($"{teamCount} existing team(s)");
        }

        var productCount = await _dbContext.Products.CountAsync(cancellationToken);
        if (productCount > 0)
        {
            summary.Add($"{productCount} existing product(s)");
        }

        var repositoryCount = await _dbContext.Repositories.CountAsync(cancellationToken);
        if (repositoryCount > 0)
        {
            summary.Add($"{repositoryCount} existing repository link(s)");
        }

        var stateClassificationCount = await _dbContext.WorkItemStateClassifications.CountAsync(cancellationToken);
        if (stateClassificationCount > 0)
        {
            summary.Add($"{stateClassificationCount} existing work item state classification(s)");
        }

        var triageTagCount = await _dbContext.TriageTags.CountAsync(cancellationToken);
        if (triageTagCount > 0)
        {
            summary.Add($"{triageTagCount} existing triage tag(s)");
        }

        var settingsCount = await _dbContext.Settings.CountAsync(cancellationToken);
        if (settingsCount > 0)
        {
            summary.Add("existing application settings");
        }

        var effortSettingsCount = await _dbContext.EffortEstimationSettings.CountAsync(cancellationToken);
        if (effortSettingsCount > 0)
        {
            summary.Add("existing effort estimation settings");
        }

        var tfsConfigCount = await _dbContext.TfsConfigs.CountAsync(cancellationToken);
        if (tfsConfigCount > 0)
        {
            summary.Add("existing TFS configuration");
        }

        return summary;
    }

    private async Task<List<string>> WipeExistingConfigurationAsync(CancellationToken cancellationToken)
    {
        var removedItems = new List<string>();

        var repositories = await _dbContext.Repositories.ToListAsync(cancellationToken);
        if (repositories.Count > 0)
        {
            _dbContext.Repositories.RemoveRange(repositories);
            removedItems.Add($"Removed {repositories.Count} repository link(s).");
        }

        var productTeamLinks = await _dbContext.ProductTeamLinks.ToListAsync(cancellationToken);
        if (productTeamLinks.Count > 0)
        {
            _dbContext.ProductTeamLinks.RemoveRange(productTeamLinks);
            removedItems.Add($"Removed {productTeamLinks.Count} product-team link(s).");
        }

        var backlogRoots = await _dbContext.ProductBacklogRoots.ToListAsync(cancellationToken);
        if (backlogRoots.Count > 0)
        {
            _dbContext.ProductBacklogRoots.RemoveRange(backlogRoots);
            removedItems.Add($"Removed {backlogRoots.Count} backlog root link(s).");
        }

        var products = await _dbContext.Products.ToListAsync(cancellationToken);
        if (products.Count > 0)
        {
            _dbContext.Products.RemoveRange(products);
            removedItems.Add($"Removed {products.Count} product(s).");
        }

        var teams = await _dbContext.Teams.ToListAsync(cancellationToken);
        if (teams.Count > 0)
        {
            _dbContext.Teams.RemoveRange(teams);
            removedItems.Add($"Removed {teams.Count} team(s).");
        }

        var profiles = await _dbContext.Profiles.ToListAsync(cancellationToken);
        if (profiles.Count > 0)
        {
            _dbContext.Profiles.RemoveRange(profiles);
            removedItems.Add($"Removed {profiles.Count} profile(s).");
        }

        var settings = await _dbContext.Settings.ToListAsync(cancellationToken);
        if (settings.Count > 0)
        {
            _dbContext.Settings.RemoveRange(settings);
            removedItems.Add($"Removed {settings.Count} application setting record(s).");
        }

        var effortSettings = await _dbContext.EffortEstimationSettings.ToListAsync(cancellationToken);
        if (effortSettings.Count > 0)
        {
            _dbContext.EffortEstimationSettings.RemoveRange(effortSettings);
            removedItems.Add($"Removed {effortSettings.Count} effort estimation setting record(s).");
        }

        var stateClassifications = await _dbContext.WorkItemStateClassifications.ToListAsync(cancellationToken);
        if (stateClassifications.Count > 0)
        {
            _dbContext.WorkItemStateClassifications.RemoveRange(stateClassifications);
            removedItems.Add($"Removed {stateClassifications.Count} work item state classification(s).");
        }

        var triageTags = await _dbContext.TriageTags.ToListAsync(cancellationToken);
        if (triageTags.Count > 0)
        {
            _dbContext.TriageTags.RemoveRange(triageTags);
            removedItems.Add($"Removed {triageTags.Count} triage tag(s).");
        }

        var tfsConfigs = await _dbContext.TfsConfigs.ToListAsync(cancellationToken);
        if (tfsConfigs.Count > 0)
        {
            _dbContext.TfsConfigs.RemoveRange(tfsConfigs);
            removedItems.Add($"Removed {tfsConfigs.Count} TFS configuration record(s).");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var removedItem in removedItems)
        {
            _logger.LogInformation("Configuration wipe step: {RemovedItem}", removedItem);
        }

        return removedItems;
    }

    private static void ValidateTeamSchema(
        ConfigurationExportDto configuration,
        ISet<int> importableTeamIds,
        ICollection<string> errors,
        ICollection<ConfigurationImportEntityResultDto> teamsImported)
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
                teamsImported.Add(CreateEntityResult(
                    TeamEntityType,
                    "Unnamed team",
                    ConfigurationImportEntityStatus.Error,
                    "A team in the import file is missing its name."));
                continue;
            }

            if (duplicateNames.Contains(team.Name))
            {
                errors.Add($"Team '{team.Name}' appears multiple times in the import file.");
                teamsImported.Add(CreateEntityResult(
                    TeamEntityType,
                    team.Name,
                    ConfigurationImportEntityStatus.Error,
                    "The team appears multiple times in the import file."));
                continue;
            }

            importableTeamIds.Add(team.Id);
        }
    }

    private sealed class ValidationContext
    {
        public required ConfigurationExportDto Configuration { get; init; }
        public TfsConfigEntity? BackupConfiguration { get; init; }
        public int? ImportedConfigurationId { get; init; }
        public bool CanImport { get; init; }
        public required IReadOnlyList<string> ValidatedProfiles { get; init; }
        public required ISet<int> ImportableProfileIds { get; init; }
        public required ISet<int> ImportableTeamIds { get; init; }
        public required IReadOnlyDictionary<int, List<int>> ValidBacklogRootIdsByProduct { get; init; }
        public required IReadOnlyDictionary<int, List<string>> ValidRepositoryNamesByProduct { get; init; }
        public required IReadOnlyDictionary<int, List<int>> ValidTeamIdsByProduct { get; init; }
        public required IReadOnlyList<string> ExistingConfigurationSummary { get; init; }
        public required IReadOnlyList<string> Warnings { get; init; }
        public required IReadOnlyList<string> Errors { get; init; }
        public required IReadOnlyList<ConfigurationImportEntityResultDto> StructuredProfilesImported { get; init; }
        public required IReadOnlyList<ConfigurationImportEntityResultDto> ProductsImported { get; init; }
        public required IReadOnlyList<ConfigurationImportEntityResultDto> TeamsImported { get; init; }
        public required IReadOnlyList<ConfigurationImportEntityResultDto> RepositoriesLinked { get; init; }
        public required IReadOnlyList<ConfigurationImportEntityResultDto> GlobalSettingsApplied { get; init; }
        public bool ExistingConfigurationDetected => ExistingConfigurationSummary.Count > 0;

        public static ValidationContext Create(
            ConfigurationExportDto configuration,
            TfsConfigEntity? backupConfiguration,
            int? importedConfigurationId,
            bool canImport,
            IReadOnlyList<string> validatedProfiles,
            ISet<int> importableProfileIds,
            ISet<int> importableTeamIds,
            IReadOnlyDictionary<int, List<int>> validBacklogRootIdsByProduct,
            IReadOnlyDictionary<int, List<string>> validRepositoryNamesByProduct,
            IReadOnlyDictionary<int, List<int>> validTeamIdsByProduct,
            IReadOnlyList<string> existingConfigurationSummary,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors,
            IReadOnlyList<ConfigurationImportEntityResultDto> structuredProfilesImported,
            IReadOnlyList<ConfigurationImportEntityResultDto> productsImported,
            IReadOnlyList<ConfigurationImportEntityResultDto> teamsImported,
            IReadOnlyList<ConfigurationImportEntityResultDto> repositoriesLinked,
            IReadOnlyList<ConfigurationImportEntityResultDto> globalSettingsApplied)
        {
            return new ValidationContext
            {
                Configuration = configuration,
                BackupConfiguration = backupConfiguration,
                ImportedConfigurationId = importedConfigurationId,
                CanImport = canImport,
                ValidatedProfiles = validatedProfiles,
                ImportableProfileIds = importableProfileIds,
                ImportableTeamIds = importableTeamIds,
                ValidBacklogRootIdsByProduct = validBacklogRootIdsByProduct,
                ValidRepositoryNamesByProduct = validRepositoryNamesByProduct,
                ValidTeamIdsByProduct = validTeamIdsByProduct,
                ExistingConfigurationSummary = existingConfigurationSummary,
                Warnings = warnings,
                Errors = errors,
                StructuredProfilesImported = structuredProfilesImported,
                ProductsImported = productsImported,
                TeamsImported = teamsImported,
                RepositoriesLinked = repositoriesLinked,
                GlobalSettingsApplied = globalSettingsApplied
            };
        }

        public static ValidationContext Invalid(
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            ConfigurationExportDto? configuration = null,
            TfsConfigEntity? backupConfiguration = null,
            IReadOnlyList<ConfigurationImportEntityResultDto>? structuredProfilesImported = null,
            IReadOnlyList<ConfigurationImportEntityResultDto>? productsImported = null,
            IReadOnlyList<ConfigurationImportEntityResultDto>? teamsImported = null,
            IReadOnlyList<ConfigurationImportEntityResultDto>? repositoriesLinked = null,
            IReadOnlyList<ConfigurationImportEntityResultDto>? globalSettingsApplied = null)
        {
            return Create(
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
                importedConfigurationId: null,
                canImport: false,
                [],
                new HashSet<int>(),
                new HashSet<int>(),
                new Dictionary<int, List<int>>(),
                new Dictionary<int, List<string>>(),
                new Dictionary<int, List<int>>(),
                [],
                warnings,
                errors,
                structuredProfilesImported ?? [],
                productsImported ?? [],
                teamsImported ?? [],
                repositoriesLinked ?? [],
                globalSettingsApplied ?? []);
        }

        public ConfigurationImportResultDto ToResult(bool importExecuted, bool requiresDestructiveConfirmation = false)
        {
            return new ConfigurationImportResultDto(
                CanImport,
                importExecuted,
                ExistingConfigurationDetected,
                requiresDestructiveConfirmation,
                ValidatedProfiles.ToList(),
                [],
                ExistingConfigurationSummary.ToList(),
                [],
                Warnings.ToList(),
                Errors.ToList(),
                StructuredProfilesImported.ToList(),
                ProductsImported.ToList(),
                TeamsImported.ToList(),
                RepositoriesLinked.ToList(),
                GlobalSettingsApplied.ToList());
        }
    }

    private static string DescribeName(string? name, string fallbackName)
    {
        return string.IsNullOrWhiteSpace(name)
            ? fallbackName
            : name;
    }

    private static string DescribeRepositoryName(string? repositoryName, string? productName)
    {
        var resolvedRepositoryName = DescribeName(repositoryName, "Unnamed repository");
        var resolvedProductName = DescribeName(productName, "Unnamed product");
        return $"{resolvedRepositoryName} ({resolvedProductName})";
    }

    private async Task<ProjectEntity> EnsureImportedProjectAsync(string? projectName, CancellationToken cancellationToken)
    {
        var normalizedProjectName = string.IsNullOrWhiteSpace(projectName)
            ? "Project"
            : projectName.Trim();

        var existing = await _dbContext.Projects.FirstOrDefaultAsync(
            project => project.Id == normalizedProjectName,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var alias = await ProjectAliasGenerator.GenerateUniqueAliasAsync(
            normalizedProjectName,
            candidate => _dbContext.Projects.AnyAsync(project => project.Alias == candidate, cancellationToken));

        var projectEntity = new ProjectEntity
        {
            Id = normalizedProjectName,
            Alias = alias,
            Name = normalizedProjectName
        };

        _dbContext.Projects.Add(projectEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return projectEntity;
    }

    private static ConfigurationImportEntityResultDto CreateEntityResult(
        string entityType,
        string name,
        ConfigurationImportEntityStatus status,
        string? message = null)
    {
        return new ConfigurationImportEntityResultDto(entityType, name, status, message);
    }

    private sealed record AppliedImportedTfsConfiguration(
        TfsConfigEntity? BackupConfiguration,
        int ConfigurationId);
}
