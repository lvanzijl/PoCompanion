using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;
using PoTool.Api.Services.Configuration;
using PoTool.Shared.BugTriage;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ExportConfigurationServiceTests
{
    private PoToolDbContext _dbContext = null!;
    private ExportConfigurationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ExportConfigurationServiceTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PoToolDbContext(options);
        _service = new ExportConfigurationService(
            _dbContext,
            new ProfileRepository(_dbContext),
            new ProductRepository(_dbContext),
            new TeamRepository(_dbContext),
            new SettingsRepository(_dbContext));
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task ExportAsync_IncludesConfigurationProfilesTeamsProductsAndSettings()
    {
        var profile = new ProfileEntity
        {
            Id = 7,
            Name = "Lesley",
            GoalIds = "11,22",
            PictureType = (int)ProfilePictureType.Default,
            DefaultPictureId = 2,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            LastModified = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var team = new TeamEntity
        {
            Id = 14,
            Name = "Delivery Team",
            TeamAreaPath = "Project\\Delivery Team",
            TfsTeamId = "team-14",
            TfsTeamName = "Delivery Team",
            ProjectName = "Project",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-4),
            LastModified = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var product = new ProductEntity
        {
            Id = 9,
            ProductOwnerId = profile.Id,
            Name = "Import Product",
            Order = 3,
            PictureType = (int)ProductPictureType.Default,
            DefaultPictureId = 4,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
            LastModified = DateTimeOffset.UtcNow.AddDays(-1)
        };

        _dbContext.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/example",
            Project = "Project",
            DefaultAreaPath = "Project",
            ApiVersion = "7.0",
            TimeoutSeconds = 30
        });
        _dbContext.Profiles.Add(profile);
        _dbContext.Teams.Add(team);
        _dbContext.Products.Add(product);
        _dbContext.ProductBacklogRoots.Add(new ProductBacklogRootEntity
        {
            ProductId = product.Id,
            WorkItemTfsId = 12345
        });
        _dbContext.ProductTeamLinks.Add(new ProductTeamLinkEntity
        {
            ProductId = product.Id,
            TeamId = team.Id
        });
        _dbContext.Repositories.Add(new RepositoryEntity
        {
            ProductId = product.Id,
            Name = "Repo-A",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });
        _dbContext.Settings.Add(new SettingsEntity
        {
            ActiveProfileId = profile.Id,
            LastModified = DateTimeOffset.UtcNow.AddHours(-2)
        });
        _dbContext.EffortEstimationSettings.Add(new EffortEstimationSettingsEntity
        {
            DefaultEffortTask = 2,
            DefaultEffortBug = 3,
            DefaultEffortUserStory = 5,
            DefaultEffortPBI = 8,
            DefaultEffortFeature = 13,
            DefaultEffortEpic = 21,
            DefaultEffortGeneric = 5,
            EnableProactiveNotifications = false,
            LastModified = DateTimeOffset.UtcNow.AddHours(-1)
        });
        _dbContext.WorkItemStateClassifications.Add(new WorkItemStateClassificationEntity
        {
            TfsProjectName = "Project",
            WorkItemType = "Epic",
            StateName = "New",
            Classification = (int)StateClassification.New
        });
        _dbContext.TriageTags.Add(new TriageTagEntity
        {
            Name = "Needs Investigation",
            IsEnabled = true,
            DisplayOrder = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.ExportAsync();

        Assert.AreEqual(ExportConfigurationService.SupportedVersion, result.Version);
        Assert.IsNotNull(result.TfsConfiguration);
        Assert.AreEqual("https://dev.azure.com/example", result.TfsConfiguration.Url);
        Assert.HasCount(1, result.Profiles);
        Assert.AreEqual("Lesley", result.Profiles[0].Name);
        Assert.HasCount(1, result.Teams);
        Assert.AreEqual("Delivery Team", result.Teams[0].Name);
        Assert.HasCount(1, result.Products);
        Assert.AreEqual("Import Product", result.Products[0].Name);
        CollectionAssert.AreEqual(new List<int> { 12345 }, result.Products[0].BacklogRootWorkItemIds);
        CollectionAssert.AreEqual(new List<int> { team.Id }, result.Products[0].TeamIds);
        Assert.AreEqual("Repo-A", result.Products[0].Repositories.Single().Name);
        Assert.IsNotNull(result.Settings);
        Assert.AreEqual(profile.Id, result.Settings.ActiveProfileId);
        Assert.IsNotNull(result.EffortEstimationSettings);
        Assert.IsFalse(result.EffortEstimationSettings.EnableProactiveNotifications);
        Assert.HasCount(1, result.StateClassifications);
        Assert.AreEqual("Project", result.StateClassifications[0].ProjectName);
        Assert.HasCount(1, result.TriageTags);
        Assert.AreEqual("Needs Investigation", result.TriageTags[0].Name);
    }
}
