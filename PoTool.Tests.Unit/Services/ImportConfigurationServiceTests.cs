using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ImportConfigurationServiceTests
{
    private PoToolDbContext _dbContext = null!;
    private Mock<ITfsClient> _tfsClientMock = null!;
    private ImportConfigurationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ImportConfigurationServiceTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PoToolDbContext(options);
        _tfsClientMock = new Mock<ITfsClient>(MockBehavior.Strict);
        _service = new ImportConfigurationService(
            _dbContext,
            _tfsClientMock.Object,
            Mock.Of<ILogger<ImportConfigurationService>>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task ImportAsync_WithValidConfiguration_ImportsProfilesProductsTeamsAndSettings()
    {
        var configuration = CreateConfigurationExportDto();
        ConfigureSuccessfulValidation();

        var result = await _service.ImportAsync(JsonSerializer.Serialize(configuration), validateOnly: false);

        Assert.IsTrue(result.CanImport);
        Assert.IsTrue(result.ImportExecuted);
        Assert.IsFalse(result.ExistingConfigurationDetected);
        Assert.IsFalse(result.RequiresDestructiveConfirmation);
        CollectionAssert.AreEquivalent(new[] { "Lesley" }, result.ProfilesImported.ToArray());

        var importedProfile = await _dbContext.Profiles.SingleAsync();
        var importedTeam = await _dbContext.Teams.SingleAsync();
        var importedProduct = await _dbContext.Products.SingleAsync();
        var settings = await _dbContext.Settings.SingleAsync();

        Assert.AreEqual("Lesley", importedProfile.Name);
        Assert.AreEqual("Delivery Team", importedTeam.Name);
        Assert.AreEqual("Import Product", importedProduct.Name);
        Assert.AreEqual(importedProfile.Id, settings.ActiveProfileId);
        Assert.AreEqual(1, await _dbContext.ProductBacklogRoots.CountAsync());
        Assert.AreEqual(1, await _dbContext.ProductTeamLinks.CountAsync());
        Assert.AreEqual(1, await _dbContext.Repositories.CountAsync());
        Assert.AreEqual(1, await _dbContext.WorkItemStateClassifications.CountAsync());
        Assert.AreEqual(1, await _dbContext.TriageTags.CountAsync());
        Assert.AreEqual("https://dev.azure.com/example", (await _dbContext.TfsConfigs.SingleAsync()).Url);
    }

    [TestMethod]
    public async Task ImportAsync_WithRepositoryIdentifierMatch_ImportsUsingCurrentRepositoryName()
    {
        var configuration = CreateConfigurationExportDto(repositoryName: "Legacy-Repo", repositoryId: "repo-1");

        ConfigureSuccessfulValidation(("Current-Repo", "repo-1"));

        var result = await _service.ImportAsync(JsonSerializer.Serialize(configuration), validateOnly: false);

        Assert.IsTrue(result.CanImport);
        Assert.IsTrue(result.ImportExecuted);
        Assert.AreEqual("Current-Repo", (await _dbContext.Repositories.SingleAsync()).Name);
        Assert.IsEmpty(result.Errors);
        Assert.IsEmpty(result.Warnings);
    }

    [TestMethod]
    public async Task ImportAsync_WithMissingRepositoryIdentifier_FallsBackToRepositoryName()
    {
        var configuration = CreateConfigurationExportDto(repositoryName: "Repo-A", repositoryId: null);

        ConfigureSuccessfulValidation(("Repo-A", "repo-1"));

        var result = await _service.ImportAsync(JsonSerializer.Serialize(configuration), validateOnly: true);

        Assert.IsTrue(result.CanImport);
        Assert.IsFalse(result.ImportExecuted);
        Assert.IsEmpty(result.Errors);
        Assert.IsEmpty(result.Warnings);
    }

    [TestMethod]
    public async Task ImportAsync_ValidateOnlyWithMissingRepository_AddsNotFoundErrorAndDoesNotPersistImportedData()
    {
        var configuration = CreateConfigurationExportDto();

        _tfsClientMock.Setup(client => client.ValidateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tfsClientMock.Setup(client => client.GetTfsProjectsAsync("https://dev.azure.com/example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TfsProjectDto("project-1", "Project", null) });
        _tfsClientMock.Setup(client => client.GetTfsTeamsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TfsTeamDto("team-1", "Delivery Team", "Project", null, "Project\\Delivery Team") });
        _tfsClientMock.Setup(client => client.GetGitRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(string Name, string Id)>());
        _tfsClientMock.Setup(client => client.GetWorkItemTypeDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WorkItemTypeDefinitionDto { TypeName = "Epic", States = new[] { "New" } } });
        _tfsClientMock.Setup(client => client.GetWorkItemByIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemDto(12345, "Epic", "Root", null, "Project\\Delivery Team", "Project\\Sprint 1", "New", DateTimeOffset.UtcNow, null, null));

        var result = await _service.ImportAsync(JsonSerializer.Serialize(configuration), validateOnly: true);

        Assert.IsTrue(result.CanImport);
        Assert.IsFalse(result.ImportExecuted);
        Assert.IsFalse(result.RequiresDestructiveConfirmation);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("Repository 'Repo-A' for product 'Import Product' was not found.", StringComparison.Ordinal)));
        Assert.AreEqual(0, await _dbContext.Profiles.CountAsync());
        Assert.AreEqual(0, await _dbContext.Products.CountAsync());
        Assert.AreEqual(0, await _dbContext.Teams.CountAsync());
        Assert.AreEqual(0, await _dbContext.TfsConfigs.CountAsync());
    }

    [TestMethod]
    public async Task ImportAsync_ValidateOnlyWithRepositoryIdentifierMismatchAndNameFallback_AddsWarning()
    {
        var configuration = CreateConfigurationExportDto(repositoryName: "Repo-A", repositoryId: "repo-old");

        ConfigureSuccessfulValidation(("Repo-A", "repo-new"));

        var result = await _service.ImportAsync(JsonSerializer.Serialize(configuration), validateOnly: true);

        Assert.IsTrue(result.CanImport);
        Assert.IsFalse(result.ImportExecuted);
        Assert.IsEmpty(result.Errors);
        Assert.IsTrue(result.Warnings.Any(warning =>
            warning.Contains("repository ID 'repo-old' was not accessible", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ImportAsync_WithExistingConfigurationAndNoWipe_RequiresConfirmationAndDoesNotImport()
    {
        _dbContext.Profiles.Add(new ProfileEntity
        {
            Name = "Existing Profile",
            GoalIds = "1"
        });
        _dbContext.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/existing",
            Project = "ExistingProject",
            DefaultAreaPath = "ExistingProject"
        });
        await _dbContext.SaveChangesAsync();

        var configuration = CreateConfigurationExportDto();
        ConfigureSuccessfulValidation();

        var result = await _service.ImportAsync(JsonSerializer.Serialize(configuration), validateOnly: false, wipeExistingConfiguration: false);

        Assert.IsTrue(result.CanImport);
        Assert.IsFalse(result.ImportExecuted);
        Assert.IsTrue(result.ExistingConfigurationDetected);
        Assert.IsTrue(result.RequiresDestructiveConfirmation);
        Assert.IsTrue(result.ExistingConfigurationSummary.Any(item => item.Contains("existing profile", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(1, await _dbContext.Profiles.CountAsync());
        Assert.AreEqual("Existing Profile", (await _dbContext.Profiles.SingleAsync()).Name);
        Assert.AreEqual("https://dev.azure.com/existing", (await _dbContext.TfsConfigs.SingleAsync()).Url);
    }

    [TestMethod]
    public async Task ImportAsync_WithExistingConfigurationAndWipe_RemovesOldConfigurationBeforeImport()
    {
        _dbContext.Profiles.Add(new ProfileEntity
        {
            Name = "Existing Profile",
            GoalIds = "1"
        });
        _dbContext.Teams.Add(new TeamEntity
        {
            Name = "Old Team",
            TeamAreaPath = "Old\\Team"
        });
        _dbContext.Products.Add(new ProductEntity
        {
            Name = "Old Product"
        });
        _dbContext.TriageTags.Add(new TriageTagEntity
        {
            Name = "Old Tag",
            IsEnabled = true,
            DisplayOrder = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _dbContext.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/existing",
            Project = "ExistingProject",
            DefaultAreaPath = "ExistingProject"
        });
        await _dbContext.SaveChangesAsync();

        var configuration = CreateConfigurationExportDto();
        ConfigureSuccessfulValidation();

        var result = await _service.ImportAsync(JsonSerializer.Serialize(configuration), validateOnly: false, wipeExistingConfiguration: true);

        Assert.IsTrue(result.ImportExecuted);
        Assert.IsTrue(result.ExistingConfigurationDetected);
        Assert.IsFalse(result.RequiresDestructiveConfirmation);
        Assert.IsTrue(result.RemovedItems.Any(item => item.Contains("profile", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.RemovedItems.Any(item => item.Contains("TFS configuration", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(1, await _dbContext.Profiles.CountAsync());
        Assert.AreEqual("Lesley", (await _dbContext.Profiles.SingleAsync()).Name);
        Assert.AreEqual(1, await _dbContext.Teams.CountAsync());
        Assert.AreEqual("Delivery Team", (await _dbContext.Teams.SingleAsync()).Name);
        Assert.AreEqual(1, await _dbContext.Products.CountAsync());
        Assert.AreEqual("Import Product", (await _dbContext.Products.SingleAsync()).Name);
        Assert.AreEqual(1, await _dbContext.TriageTags.CountAsync());
        Assert.AreEqual("Needs Investigation", (await _dbContext.TriageTags.SingleAsync()).Name);
        Assert.AreEqual("https://dev.azure.com/example", (await _dbContext.TfsConfigs.SingleAsync()).Url);
    }

    private static ConfigurationExportDto CreateConfigurationExportDto(
        string repositoryName = "Repo-A",
        string? repositoryId = "repo-1")
    {
        return new ConfigurationExportDto(
            Version: ExportConfigurationService.SupportedVersion,
            ExportedAt: DateTimeOffset.UtcNow,
            TfsConfiguration: new TfsConfigEntity
            {
                Url = "https://dev.azure.com/example",
                Project = "Project",
                DefaultAreaPath = "Project",
                ApiVersion = "7.0",
                TimeoutSeconds = 30,
                UseDefaultCredentials = true
            },
            Settings: new SettingsDto(1, 1, DateTimeOffset.UtcNow),
            EffortEstimationSettings: EffortEstimationSettingsDto.Default,
            StateClassifications: new[]
            {
                new ConfigurationStateClassificationDto("Project", "Epic", "New", StateClassification.New)
            },
            TriageTags: new[]
            {
                new PoTool.Shared.BugTriage.TriageTagDto(1, "Needs Investigation", true, 1, DateTimeOffset.UtcNow)
            },
            Profiles: new[]
            {
                new ProfileDto(1, "Lesley", new List<int> { 11 }, ProfilePictureType.Default, 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            },
            Teams: new[]
            {
                new TeamDto(1, "Delivery Team", "Project\\Delivery Team", false, TeamPictureType.Default, 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Project", "team-1", "Delivery Team", null)
            },
            Products: new[]
            {
                new ProductDto(
                    1,
                    1,
                    "Import Product",
                    new List<int> { 12345 },
                    0,
                    ProductPictureType.Default,
                    0,
                    null,
                     DateTimeOffset.UtcNow,
                     DateTimeOffset.UtcNow,
                     null,
                     new List<int> { 1 },
                     new List<RepositoryDto> { new(1, 1, repositoryName, DateTimeOffset.UtcNow, repositoryId) })
            });
    }

    private void ConfigureSuccessfulValidation(params (string Name, string Id)[] repositories)
    {
        _tfsClientMock.Setup(client => client.ValidateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tfsClientMock.Setup(client => client.GetTfsProjectsAsync("https://dev.azure.com/example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TfsProjectDto("project-1", "Project", null) });
        _tfsClientMock.Setup(client => client.GetTfsTeamsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TfsTeamDto("team-1", "Delivery Team", "Project", null, "Project\\Delivery Team") });
        _tfsClientMock.Setup(client => client.GetGitRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories.Length == 0 ? new[] { (Name: "Repo-A", Id: "repo-1") } : repositories);
        _tfsClientMock.Setup(client => client.GetWorkItemTypeDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WorkItemTypeDefinitionDto { TypeName = "Epic", States = new[] { "New" } } });
        _tfsClientMock.Setup(client => client.GetWorkItemByIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemDto(12345, "Epic", "Root", null, "Project\\Delivery Team", "Project\\Sprint 1", "New", DateTimeOffset.UtcNow, null, null));
    }
}
