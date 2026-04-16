using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.Settings;
using StartupReadinessDto = PoTool.Client.Services.StartupReadinessDto;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Unit tests for the StartupOrchestratorService decision tree.
/// Tests the startup routing logic as specified in User_landing_v2.md.
/// </summary>
[TestClass]
public class StartupOrchestratorServiceTests
{
    private StartupOrchestratorService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        int? cachedProfileId = null,
        ProfileDto? cachedProfile = null)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var preferencesService = new StubPreferencesService(cachedProfileId);
        var profileService = new StubProfileService(cachedProfile, cachedProfileId);

        return new StartupOrchestratorService(
            new StartupClient(httpClient),
            new CacheSyncService(httpClient, new CacheSyncClient(httpClient)),
            profileService,
            preferencesService);
    }

    [TestMethod]
    public async Task GetStartupReadinessAsync_HttpFailure_ReturnsUnavailableState()
    {
        var service = CreateService(_ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        var result = await service.GetStartupReadinessAsync();

        Assert.AreEqual(StartupReadinessState.Unavailable, result.State);
        Assert.IsNull(result.Readiness);
    }

    [TestMethod]
    public async Task GetStartupReadinessAsync_NoSuccessfulSync_ReturnsSyncRequired()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": true,
              "hasTestedConnectionSuccessfully": true,
              "hasVerifiedTfsApiSuccessfully": true,
              "hasAnyProfile": true,
              "activeProfileId": 7,
              "missingRequirementMessage": null
            }
            """;
        var cacheJson = """
            {
              "productOwnerId": 7,
              "syncStatus": 0,
              "lastSuccessfulSync": null,
              "lastErrorMessage": "Cache empty."
            }
            """;

        var service = CreateService(request =>
        {
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            };
        }, cachedProfileId: 7, cachedProfile: CreateProfile(7, "Marina"));

        var result = await service.GetStartupReadinessAsync();

        Assert.AreEqual(StartupReadinessState.SyncRequired, result.State);
        Assert.IsNotNull(result.Readiness);
    }

    [TestMethod]
    public async Task GetStartupReadinessAsync_MissingCachedProfileSelection_ReturnsNotReady()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": true,
              "hasTestedConnectionSuccessfully": true,
              "hasVerifiedTfsApiSuccessfully": true,
              "hasAnyProfile": true,
              "activeProfileId": 7,
              "missingRequirementMessage": null
            }
            """;

        var service = CreateService(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            });

        var result = await service.GetStartupReadinessAsync();

        Assert.AreEqual(StartupReadinessState.NotReady, result.State);
        Assert.IsNotNull(result.Readiness);
        Assert.IsNull(result.Readiness.ActiveProfileId);
    }

    [TestMethod]
    public async Task GetStartupReadinessAsync_InvalidCachedProfileSelection_ClearsAndReturnsNotReady()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": true,
              "hasTestedConnectionSuccessfully": true,
              "hasVerifiedTfsApiSuccessfully": true,
              "hasAnyProfile": true,
              "activeProfileId": 7,
              "missingRequirementMessage": null
            }
            """;

        var service = CreateService(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 42,
            cachedProfile: null);

        var result = await service.GetStartupReadinessAsync();

        Assert.AreEqual(StartupReadinessState.NotReady, result.State);
        Assert.IsNotNull(result.Readiness);
        Assert.IsNull(result.Readiness.ActiveProfileId);
    }

    [TestMethod]
    public async Task GetStartupReadinessAsync_ValidCachedProfileSelection_RestoresServerSelection()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": true,
              "hasTestedConnectionSuccessfully": true,
              "hasVerifiedTfsApiSuccessfully": true,
              "hasAnyProfile": true,
              "activeProfileId": null,
              "missingRequirementMessage": null
            }
            """;
        var cacheJson = """
            {
              "productOwnerId": 7,
              "syncStatus": 1,
              "lastSuccessfulSync": "2026-04-16T07:15:00Z",
              "lastErrorMessage": null
            }
            """;

        var cachedProfile = CreateProfile(7, "Marina");
        var service = CreateService(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7,
            cachedProfile: cachedProfile);

        var result = await service.GetStartupReadinessAsync();

        Assert.AreEqual(StartupReadinessState.Ready, result.State);
        Assert.IsNotNull(result.Readiness);
        Assert.AreEqual(7, result.Readiness.ActiveProfileId);
    }

    [TestMethod]
    public void DetermineRoute_NoTfsConfig_ReturnsConfigurationRoute()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.SetupRequired,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: false,
                HasTestedConnectionSuccessfully: false,
                HasVerifiedTfsApiSuccessfully: false,
                HasAnyProfile: false,
                ActiveProfileId: null,
                MissingRequirementMessage: "Configuration required"),
            "Configuration required",
            "Open TFS settings."
        );

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.Configuration, result.Route);
        Assert.IsNotNull(result.Message);
        Assert.Contains("Configuration", result.Message);
    }

    [TestMethod]
    public void DetermineRoute_ConfigSavedButNotTested_ReturnsConfigurationRoute()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.SetupRequired,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: true,
                HasTestedConnectionSuccessfully: false,
                HasVerifiedTfsApiSuccessfully: false,
                HasAnyProfile: false,
                ActiveProfileId: null,
                MissingRequirementMessage: "Test Connection required"),
            "Test Connection required",
            "Open TFS settings."
        );

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.Configuration, result.Route);
        Assert.Contains("Test Connection", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_TestedButNotVerified_ReturnsConfigurationRoute()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.SetupRequired,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: true,
                HasTestedConnectionSuccessfully: true,
                HasVerifiedTfsApiSuccessfully: false,
                HasAnyProfile: false,
                ActiveProfileId: null,
                MissingRequirementMessage: "Verify TFS API required"),
            "Verify TFS API required",
            "Open TFS settings."
        );

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.Configuration, result.Route);
        Assert.Contains("Verify", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_VerifiedButNoProfile_ReturnsCreateFirstProfileRoute()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.SetupRequired,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: true,
                HasTestedConnectionSuccessfully: true,
                HasVerifiedTfsApiSuccessfully: true,
                HasAnyProfile: false,
                ActiveProfileId: null,
                MissingRequirementMessage: "Profile required"),
            "Profile required",
            "Create your first profile."
        );

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.CreateFirstProfile, result.Route);
        Assert.Contains("Profile", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_HasProfileButNoneActive_ReturnsProfilesHome()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.NotReady,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: true,
                HasTestedConnectionSuccessfully: true,
                HasVerifiedTfsApiSuccessfully: true,
                HasAnyProfile: true,
                ActiveProfileId: null,
                MissingRequirementMessage: "Profile selection required"),
            "Profile selection required",
            "Open Profiles."
        );

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.ProfilesHome, result.Route);
        Assert.Contains("select", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_SyncRequired_ReturnsSyncGate()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.SyncRequired,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: true,
                HasTestedConnectionSuccessfully: true,
                HasVerifiedTfsApiSuccessfully: true,
                HasAnyProfile: true,
                ActiveProfileId: 1,
                MissingRequirementMessage: null),
            "Sync required",
            "Open sync gate."
        );

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.SyncGate, result.Route);
    }

    [TestMethod]
    public void DetermineRoute_AllRequirementsMet_ReturnsHome()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.Ready,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: true,
                HasTestedConnectionSuccessfully: true,
                HasVerifiedTfsApiSuccessfully: true,
                HasAnyProfile: true,
                ActiveProfileId: 1,
                MissingRequirementMessage: null),
            "Startup checks passed.",
            "Continue to home."
        );

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.Home, result.Route);
        Assert.IsFalse(result.IsBlocking);
    }

    [TestMethod]
    public void DetermineRoute_ErrorState_ReturnsBlockingErrorRoute()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.Error,
            Readiness: null,
            Reason: "Startup failed.",
            RecoveryHint: "Retry.");

        var result = service.DetermineRoute(readiness);

        Assert.AreEqual(StartupRoute.BlockingError, result.Route);
        Assert.IsTrue(result.IsBlocking);
    }

    [TestMethod]
    public void IsFeaturePageAccessible_Ready_ReturnsTrue()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.Ready,
            new StartupReadinessDto(
                IsMockDataEnabled: false,
                HasSavedTfsConfig: true,
                HasTestedConnectionSuccessfully: true,
                HasVerifiedTfsApiSuccessfully: true,
                HasAnyProfile: true,
                ActiveProfileId: 1,
                MissingRequirementMessage: null),
            "Ready",
            "Continue."
        );

        var result = service.IsFeaturePageAccessible(readiness);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsFeaturePageAccessible_Error_ReturnsFalse()
    {
        var service = CreateService(_ => throw new InvalidOperationException("HTTP should not be used."));
        var readiness = new StartupReadinessResult(
            StartupReadinessState.Error,
            Readiness: null,
            Reason: "Error",
            RecoveryHint: "Retry."
        );

        var result = service.IsFeaturePageAccessible(readiness);

        Assert.IsFalse(result);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
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

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class StubPreferencesService : IPreferencesService
    {
        public StubPreferencesService(int? storedIntValue)
        {
            StoredIntValue = storedIntValue;
        }

        public int? StoredIntValue { get; private set; }

        public Task<bool> GetBoolAsync(string key, bool defaultValue) => Task.FromResult(defaultValue);

        public Task SetBoolAsync(string key, bool value) => Task.CompletedTask;

        public Task<int?> GetIntAsync(string key) => Task.FromResult(StoredIntValue);

        public Task SetIntAsync(string key, int value)
        {
            StoredIntValue = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            StoredIntValue = null;
            return Task.CompletedTask;
        }
    }

    private sealed class StubProfileService : IProfileService
    {
        public StubProfileService(ProfileDto? profile, int? cachedProfileId)
        {
            Profile = profile;
            CachedActiveProfileId = cachedProfileId;
        }

        public ProfileDto? Profile { get; }

        public int? CachedActiveProfileId { get; private set; }

        public Task<IEnumerable<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ProfileDto>>(Profile is null ? [] : [Profile]);

        public Task<ProfileDto?> GetProfileByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Profile?.Id == id ? Profile : null);

        public Task<ProfileDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Profile?.Id == CachedActiveProfileId ? Profile : null);

        public Task<ProfileDto> CreateProfileAsync(string name, List<int> goalIds, ProfilePictureType pictureType = ProfilePictureType.Default, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProfileDto> UpdateProfileAsync(int id, string name, List<int> goalIds, ProfilePictureType? pictureType = null, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default)
        {
            CachedActiveProfileId = profileId;
            return Task.FromResult(new SettingsDto(1, profileId, DateTimeOffset.UtcNow));
        }

        public Task<ProfileDto> CreateAndActivateProfileAsync(string name, List<int> goalIds, ProfilePictureType pictureType = ProfilePictureType.Default, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public int? GetActiveProfileId() => CachedActiveProfileId;

        public bool IsActiveProfileValid() => CachedActiveProfileId.HasValue;

        public void SetCachedActiveProfileId(int? profileId)
        {
            CachedActiveProfileId = profileId;
        }
    }
}
