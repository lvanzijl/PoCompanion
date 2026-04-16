using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class StartupOrchestratorServiceTests
{
    [TestMethod]
    public async Task ResolveStartupStateAsync_BackendFailure_ReturnsBlockedState()
    {
        var (_, service, _, _) = CreateSystemUnderTest(
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        var result = await service.ResolveStartupStateAsync("home");

        Assert.AreEqual(StartupResolutionState.Blocked, result.State);
        Assert.AreEqual(StartupBlockedReason.BackendUnavailable, result.BlockedReason);
        Assert.AreEqual("/startup-blocked?message=Startup%20readiness%20is%20unavailable%20%28HTTP%20503%29.&hint=Retry%20after%20confirming%20the%20backend%20is%20available.", result.TargetUri);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_MissingConfiguration_ReturnsBlockedState()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": false,
              "hasTestedConnectionSuccessfully": false,
              "hasVerifiedTfsApiSuccessfully": false,
              "hasAnyProfile": false,
              "activeProfileId": null,
              "missingRequirementMessage": "Configuration required."
            }
            """;

        var (_, service, preferences, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7);

        var result = await service.ResolveStartupStateAsync("/");

        Assert.AreEqual(StartupResolutionState.Blocked, result.State);
        Assert.AreEqual(StartupBlockedReason.MissingConfiguration, result.BlockedReason);
        Assert.IsNull(preferences.StoredIntValue);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_NoProfiles_ReturnsNoProfile()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": true,
              "hasTestedConnectionSuccessfully": true,
              "hasVerifiedTfsApiSuccessfully": true,
              "hasAnyProfile": false,
              "activeProfileId": null,
              "missingRequirementMessage": null
            }
            """;

        var (_, service, _, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            });

        var result = await service.ResolveStartupStateAsync("home/delivery/execution?sprintId=7");

        Assert.AreEqual(StartupResolutionState.NoProfile, result.State);
        Assert.AreEqual("/profiles?returnUrl=%2Fhome%2Fdelivery%2Fexecution%3FsprintId%3D7", result.TargetUri);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_InvalidServerProfile_ReturnsProfileInvalid()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": true,
              "hasTestedConnectionSuccessfully": true,
              "hasVerifiedTfsApiSuccessfully": true,
              "hasAnyProfile": true,
              "activeProfileId": 42,
              "missingRequirementMessage": null
            }
            """;

        var (profileService, service, preferences, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 42);

        var result = await service.ResolveStartupStateAsync("/home");

        Assert.AreEqual(StartupResolutionState.ProfileInvalid, result.State);
        Assert.IsNull(profileService.LastSetActiveProfileId);
        Assert.IsNull(preferences.StoredIntValue);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_ServerProfileWinsClientMismatch()
    {
        var readinessJson = """
            {
              "isMockDataEnabled": false,
              "hasSavedTfsConfig": true,
              "hasTestedConnectionSuccessfully": true,
              "hasVerifiedTfsApiSuccessfully": true,
              "hasAnyProfile": true,
              "activeProfileId": 9,
              "missingRequirementMessage": null
            }
            """;
        var cacheJson = """
            {
              "productOwnerId": 9,
              "syncStatus": 2,
              "lastAttemptSync": "2026-04-16T07:15:01Z",
              "lastSuccessfulSync": "2026-04-16T07:15:00Z",
              "workItemCount": 4,
              "pullRequestCount": 1,
              "pipelineCount": 1,
              "workItemWatermark": "2026-04-16T07:14:00Z",
              "pullRequestWatermark": "2026-04-16T07:14:00Z",
              "pipelineFinishWatermark": "2026-04-16T07:14:00Z"
            }
            """;

        var profiles = new[]
        {
            CreateProfile(7, "Alice"),
            CreateProfile(9, "Bob")
        };

        var (_, service, preferences, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/9" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7,
            profiles: profiles);

        var result = await service.ResolveStartupStateAsync("/home");

        Assert.AreEqual(StartupResolutionState.Ready, result.State);
        Assert.AreEqual(9, result.ActiveProfileId);
        Assert.AreEqual(9, preferences.StoredIntValue);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_ValidHintRestoresServerSelection()
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
              "syncStatus": 2,
              "lastAttemptSync": "2026-04-16T07:15:02Z",
              "lastSuccessfulSync": "2026-04-16T07:15:00Z",
              "workItemCount": 2,
              "pullRequestCount": 0,
              "pipelineCount": 0,
              "workItemWatermark": "2026-04-16T07:14:00Z",
              "pullRequestWatermark": null,
              "pipelineFinishWatermark": null
            }
            """;

        var (profileService, service, preferences, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7,
            profiles: new[] { CreateProfile(7, "Marina") });

        var result = await service.ResolveStartupStateAsync("/home");

        Assert.AreEqual(StartupResolutionState.Ready, result.State);
        Assert.AreEqual(7, profileService.LastSetActiveProfileId);
        Assert.AreEqual(7, preferences.StoredIntValue);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_NoSuccessfulSync_ReturnsProfileValidNoSync()
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
              "lastSuccessfulSync": null
            }
            """;

        var (_, service, _, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7,
            profiles: new[] { CreateProfile(7, "Marina") });

        var result = await service.ResolveStartupStateAsync("/home/pipeline-insights");

        Assert.AreEqual(StartupResolutionState.ProfileValid_NoSync, result.State);
        Assert.AreEqual("/sync-gate?returnUrl=%2Fhome%2Fpipeline-insights", result.TargetUri);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_LaterFailedAttemptRejectsStaleSync()
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
              "syncStatus": 2,
              "lastAttemptSync": "2026-04-16T07:30:30Z",
              "lastSuccessfulSync": "2026-04-16T07:15:00Z",
              "workItemCount": 4,
              "pullRequestCount": 1,
              "pipelineCount": 1,
              "workItemWatermark": "2026-04-16T07:14:00Z"
            }
            """;

        var (_, service, _, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7,
            profiles: new[] { CreateProfile(7, "Marina") });

        var result = await service.ResolveStartupStateAsync("/home");

        Assert.AreEqual(StartupResolutionState.ProfileValid_NoSync, result.State);
        StringAssert.Contains(result.Reason, "invalidated");
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_MissingWatermarkRejectsPartialSync()
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
              "syncStatus": 2,
              "lastAttemptSync": "2026-04-16T07:15:01Z",
              "lastSuccessfulSync": "2026-04-16T07:15:00Z",
              "workItemCount": 4,
              "pullRequestCount": 1,
              "pipelineCount": 1,
              "workItemWatermark": null
            }
            """;

        var (_, service, _, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7,
            profiles: new[] { CreateProfile(7, "Marina") });

        var result = await service.ResolveStartupStateAsync("/home");

        Assert.AreEqual(StartupResolutionState.ProfileValid_NoSync, result.State);
        StringAssert.Contains(result.Reason, "work item watermark");
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_DeepLinkReady_PreservesRequestedRoute()
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
              "syncStatus": 2,
              "lastAttemptSync": "2026-04-16T07:15:02Z",
              "lastSuccessfulSync": "2026-04-16T07:15:00Z",
              "workItemCount": 2,
              "pullRequestCount": 0,
              "pipelineCount": 0,
              "workItemWatermark": "2026-04-16T07:14:00Z"
            }
            """;

        var (_, service, _, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/Startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            cachedProfileId: 7,
            profiles: new[] { CreateProfile(7, "Marina") });

        var result = await service.ResolveStartupStateAsync("/home/delivery/execution?sprintId=3");

        Assert.AreEqual(StartupResolutionState.Ready, result.State);
        Assert.AreEqual("/home/delivery/execution?sprintId=3", result.TargetUri);
    }

    private static (StubProfileService ProfileService, StartupOrchestratorService Service, StubPreferencesService Preferences, HttpClient HttpClient) CreateSystemUnderTest(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        int? cachedProfileId = null,
        IEnumerable<ProfileDto>? profiles = null)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var preferencesService = new StubPreferencesService(cachedProfileId);
        var profileService = new StubProfileService(profiles ?? Array.Empty<ProfileDto>(), cachedProfileId);

        var service = new StartupOrchestratorService(
            new StartupClient(httpClient),
            new CacheSyncService(httpClient, new CacheSyncClient(httpClient)),
            profileService,
            preferencesService);

        return (profileService, service, preferencesService, httpClient);
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
        private readonly Dictionary<int, ProfileDto> _profiles;

        public StubProfileService(IEnumerable<ProfileDto> profiles, int? cachedProfileId)
        {
            _profiles = profiles.ToDictionary(profile => profile.Id);
            CachedActiveProfileId = cachedProfileId;
        }

        public int? CachedActiveProfileId { get; private set; }

        public int? LastSetActiveProfileId { get; private set; }

        public Task<IEnumerable<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ProfileDto>>(_profiles.Values);

        public Task<ProfileDto?> GetProfileByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_profiles.GetValueOrDefault(id));

        public Task<ProfileDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CachedActiveProfileId.HasValue ? _profiles.GetValueOrDefault(CachedActiveProfileId.Value) : null);

        public Task<ProfileDto> CreateProfileAsync(string name, List<int> goalIds, ProfilePictureType pictureType = ProfilePictureType.Default, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProfileDto> UpdateProfileAsync(int id, string name, List<int> goalIds, ProfilePictureType? pictureType = null, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default)
        {
            CachedActiveProfileId = profileId;
            LastSetActiveProfileId = profileId;
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
