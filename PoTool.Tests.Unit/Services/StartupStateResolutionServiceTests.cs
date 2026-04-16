using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class StartupStateResolutionServiceTests
{
    [TestMethod]
    public async Task ResolveAsync_MissingConfigurationInRealMode_ReturnsBlocked()
    {
        var service = CreateService(
            configEntity: null,
            runtimeMode: new TfsRuntimeMode(useMockClient: false),
            hasAnyProfile: false);

        var result = await service.ResolveAsync("/home", profileHintId: null);

        Assert.AreEqual(StartupStateDto.Blocked, result.StartupState);
        Assert.AreEqual("/startup-blocked", result.TargetRoute);
        Assert.AreEqual(StartupBlockedReasonDto.MissingConfiguration, result.BlockedReason);
    }

    [TestMethod]
    public async Task ResolveAsync_NoProfiles_ReturnsNoProfile()
    {
        var service = CreateService(
            configEntity: CreateVerifiedConfig(),
            runtimeMode: new TfsRuntimeMode(useMockClient: false),
            hasAnyProfile: false);

        var result = await service.ResolveAsync("/home/delivery/execution?sprintId=7", profileHintId: null);

        Assert.AreEqual(StartupStateDto.NoProfile, result.StartupState);
        Assert.AreEqual("/profiles?returnUrl=%2Fhome%2Fdelivery%2Fexecution%3FsprintId%3D7", result.TargetRoute);
        Assert.AreEqual("/home/delivery/execution?sprintId=7", result.ReturnUrl);
    }

    [TestMethod]
    public async Task ResolveAsync_InvalidPersistedProfile_ClearsServerSelection_AndReturnsProfileInvalid()
    {
        var settingsState = new SettingsDto(1, 42, DateTimeOffset.UtcNow);
        var settingsRepository = CreateSettingsRepository(settingsState);
        var service = CreateService(
            configEntity: CreateVerifiedConfig(),
            runtimeMode: new TfsRuntimeMode(useMockClient: false),
            hasAnyProfile: true,
            settingsRepository: settingsRepository.Object);

        var result = await service.ResolveAsync("/home", profileHintId: 42);

        Assert.AreEqual(StartupStateDto.ProfileInvalid, result.StartupState);
        settingsRepository.Verify(repository => repository.SetActiveProfileAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ResolveAsync_ValidClientHintRestoresServerSelection_AndRoutesToSyncGate()
    {
        var profiles = new Dictionary<int, ProfileDto>
        {
            [7] = CreateProfile(7, "Marina")
        };
        var settingsRepository = CreateSettingsRepository(new SettingsDto(1, null, DateTimeOffset.UtcNow));
        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository.Setup(repository => repository.GetCacheStateAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheStateDto
            {
                ProductOwnerId = 7,
                SyncStatus = CacheSyncStatusDto.InProgress
            });

        var service = CreateService(
            configEntity: CreateVerifiedConfig(),
            runtimeMode: new TfsRuntimeMode(useMockClient: false),
            hasAnyProfile: true,
            profiles: profiles,
            settingsRepository: settingsRepository.Object,
            cacheStateRepository: cacheStateRepository.Object);

        var result = await service.ResolveAsync("/home/pipeline-insights", profileHintId: 7);

        Assert.AreEqual(StartupStateDto.ProfileValid_NoSync, result.StartupState);
        Assert.AreEqual("/sync-gate?returnUrl=%2Fhome%2Fpipeline-insights", result.TargetRoute);
        settingsRepository.Verify(repository => repository.SetActiveProfileAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsTrue(result.Diagnostics.ClientHintApplied);
    }

    [TestMethod]
    public async Task ResolveAsync_StaleSuccessfulSync_ReturnsInvalidatedNoSync()
    {
        var profiles = new Dictionary<int, ProfileDto>
        {
            [7] = CreateProfile(7, "Marina")
        };
        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository.Setup(repository => repository.GetCacheStateAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheStateDto
            {
                ProductOwnerId = 7,
                SyncStatus = CacheSyncStatusDto.Success,
                LastAttemptSync = DateTimeOffset.Parse("2026-04-16T07:30:30Z"),
                LastSuccessfulSync = DateTimeOffset.Parse("2026-04-16T07:15:00Z"),
                WorkItemWatermark = DateTimeOffset.Parse("2026-04-16T07:14:00Z")
            });

        var service = CreateService(
            configEntity: CreateVerifiedConfig(),
            runtimeMode: new TfsRuntimeMode(useMockClient: false),
            hasAnyProfile: true,
            profiles: profiles,
            settingsRepository: CreateSettingsRepository(new SettingsDto(1, 7, DateTimeOffset.UtcNow)).Object,
            cacheStateRepository: cacheStateRepository.Object);

        var result = await service.ResolveAsync("/home", profileHintId: null);

        Assert.AreEqual(StartupStateDto.ProfileValid_NoSync, result.StartupState);
        Assert.AreEqual(StartupSyncStatusDto.Invalidated, result.SyncStatus);
    }

    [TestMethod]
    public async Task ResolveAsync_ValidReadyDeepLink_ReturnsReadyWithPreservedRoute()
    {
        var profiles = new Dictionary<int, ProfileDto>
        {
            [9] = CreateProfile(9, "Bob")
        };
        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository.Setup(repository => repository.GetCacheStateAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheStateDto
            {
                ProductOwnerId = 9,
                SyncStatus = CacheSyncStatusDto.Success,
                LastAttemptSync = DateTimeOffset.Parse("2026-04-16T07:15:02Z"),
                LastSuccessfulSync = DateTimeOffset.Parse("2026-04-16T07:15:00Z"),
                WorkItemWatermark = DateTimeOffset.Parse("2026-04-16T07:14:00Z")
            });

        var service = CreateService(
            configEntity: CreateVerifiedConfig(),
            runtimeMode: new TfsRuntimeMode(useMockClient: false),
            hasAnyProfile: true,
            profiles: profiles,
            settingsRepository: CreateSettingsRepository(new SettingsDto(1, 9, DateTimeOffset.UtcNow)).Object,
            cacheStateRepository: cacheStateRepository.Object);

        var result = await service.ResolveAsync("/home/delivery/execution?sprintId=3", profileHintId: 7);

        Assert.AreEqual(StartupStateDto.Ready, result.StartupState);
        Assert.AreEqual("/home/delivery/execution?sprintId=3", result.TargetRoute);
        Assert.IsTrue(result.Diagnostics.ClientHintRejected);
    }

    private static StartupStateResolutionService CreateService(
        TfsConfigEntity? configEntity,
        TfsRuntimeMode runtimeMode,
        bool hasAnyProfile,
        Dictionary<int, ProfileDto>? profiles = null,
        ISettingsRepository? settingsRepository = null,
        ICacheStateRepository? cacheStateRepository = null)
    {
        var profileRepository = new Mock<IProfileRepository>();
        profileRepository.Setup(repository => repository.HasAnyProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasAnyProfile);
        profileRepository.Setup(repository => repository.GetProfileByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                profiles ??= [];
                return profiles.GetValueOrDefault(id);
            });

        return new StartupStateResolutionService(
            CreateTfsConfigurationService(configEntity),
            profileRepository.Object,
            settingsRepository ?? CreateSettingsRepository(new SettingsDto(1, null, DateTimeOffset.UtcNow)).Object,
            cacheStateRepository ?? Mock.Of<ICacheStateRepository>(),
            runtimeMode);
    }

    private static TfsConfigurationService CreateTfsConfigurationService(TfsConfigEntity? configEntity)
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var dbContext = new PoToolDbContext(options);
        if (configEntity != null)
        {
            dbContext.TfsConfigs.Add(configEntity);
            dbContext.SaveChanges();
        }

        return new TfsConfigurationService(
            dbContext,
            Mock.Of<ILogger<TfsConfigurationService>>(),
            new PassThroughEfConcurrencyGate());
    }

    private static Mock<ISettingsRepository> CreateSettingsRepository(SettingsDto currentSettings)
    {
        var state = currentSettings;
        var settingsRepository = new Mock<ISettingsRepository>();
        settingsRepository.Setup(repository => repository.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => state);
        settingsRepository.Setup(repository => repository.SetActiveProfileAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int? profileId, CancellationToken _) =>
            {
                state = new SettingsDto(state.Id, profileId, DateTimeOffset.UtcNow);
                return state;
            });

        return settingsRepository;
    }

    private static TfsConfigEntity CreateVerifiedConfig()
    {
        return new TfsConfigEntity
        {
            Url = "https://dev.azure.local",
            Project = "Po Companion",
            DefaultAreaPath = "Po Companion",
            HasTestedConnectionSuccessfully = true,
            HasVerifiedTfsApiSuccessfully = true
        };
    }

    private static ProfileDto CreateProfile(int id, string name)
    {
        return new ProfileDto(
            id,
            name,
            [],
            ProfilePictureType.Default,
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class PassThroughEfConcurrencyGate : IEfConcurrencyGate
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
            => operation();

        public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
            => operation();
    }
}
