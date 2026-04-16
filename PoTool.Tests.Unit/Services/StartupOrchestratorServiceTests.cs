using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class StartupOrchestratorServiceTests
{
    [TestMethod]
    public async Task ResolveStartupStateAsync_BackendFailure_ReturnsBlockingRoute()
    {
        var (_, service, _, _) = CreateSystemUnderTest(
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        var result = await service.ResolveStartupStateAsync("home");

        Assert.IsNull(result.Contract);
        Assert.AreEqual("/startup-blocked", result.TargetUri);
        Assert.IsFalse(result.ShouldRenderCurrentRoute);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_NoProfile_UsesAuthoritativeProfilesRoute()
    {
        var contractJson = """
            {
              "startupState": 0,
              "targetRoute": "/profiles?returnUrl=%2Fhome%2Fdelivery%2Fexecution%3FsprintId%3D7",
              "returnUrl": "/home/delivery/execution?sprintId=7",
              "activeProfileId": null,
              "syncStatus": 0,
              "blockedReason": null,
              "diagnostics": {
                "hasSavedTfsConfig": true,
                "hasTestedConnectionSuccessfully": true,
                "hasVerifiedTfsApiSuccessfully": true,
                "hasAnyProfile": true,
                "serverActiveProfilePresent": false,
                "clientHintProvided": false,
                "clientHintApplied": false,
                "clientHintRejected": false,
                "cacheStatePresent": false,
                "syncCompletedSuccessfully": false,
                "syncDataPresent": false,
                "syncAttemptWithinTolerance": false
              }
            }
            """;

        var (_, service, _, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/startup-state" => CreateJsonResponse(contractJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            });

        var result = await service.ResolveStartupStateAsync("home/delivery/execution?sprintId=7");

        Assert.IsNotNull(result.Contract);
        Assert.AreEqual(StartupStateDto.NoProfile, result.Contract.StartupState);
        Assert.AreEqual("/profiles?returnUrl=%2Fhome%2Fdelivery%2Fexecution%3FsprintId%3D7", result.TargetUri);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_ProfileInvalid_ClearsClientHint()
    {
        var contractJson = """
            {
              "startupState": 1,
              "targetRoute": "/profiles?returnUrl=%2Fhome",
              "returnUrl": "/home",
              "activeProfileId": null,
              "syncStatus": 0,
              "blockedReason": null,
              "diagnostics": {
                "hasSavedTfsConfig": true,
                "hasTestedConnectionSuccessfully": true,
                "hasVerifiedTfsApiSuccessfully": true,
                "hasAnyProfile": true,
                "serverActiveProfilePresent": false,
                "clientHintProvided": true,
                "clientHintApplied": false,
                "clientHintRejected": true,
                "cacheStatePresent": false,
                "syncCompletedSuccessfully": false,
                "syncDataPresent": false,
                "syncAttemptWithinTolerance": false
              }
            }
            """;

        var (profileService, service, preferences, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/startup-state" => CreateJsonResponse(contractJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            storedProfileHintId: 42);

        var result = await service.ResolveStartupStateAsync("/home");

        Assert.AreEqual(StartupStateDto.ProfileInvalid, result.Contract?.StartupState);
        Assert.IsNull(preferences.StoredIntValue);
        Assert.IsNull(profileService.CachedActiveProfileId);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_ProfileValidNoSync_UsesSyncGateRoute()
    {
        var contractJson = """
            {
              "startupState": 2,
              "targetRoute": "/sync-gate?returnUrl=%2Fhome%2Fpipeline-insights",
              "returnUrl": "/home/pipeline-insights",
              "activeProfileId": 7,
              "syncStatus": 1,
              "blockedReason": null,
              "diagnostics": {
                "hasSavedTfsConfig": true,
                "hasTestedConnectionSuccessfully": true,
                "hasVerifiedTfsApiSuccessfully": true,
                "hasAnyProfile": true,
                "serverActiveProfilePresent": true,
                "clientHintProvided": true,
                "clientHintApplied": false,
                "clientHintRejected": false,
                "cacheStatePresent": true,
                "syncCompletedSuccessfully": false,
                "syncDataPresent": false,
                "syncAttemptWithinTolerance": false
              }
            }
            """;

        var (_, service, preferences, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/startup-state" => CreateJsonResponse(contractJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            storedProfileHintId: 7);

        var result = await service.ResolveStartupStateAsync("/home/pipeline-insights");

        Assert.AreEqual(StartupStateDto.ProfileValid_NoSync, result.Contract?.StartupState);
        Assert.AreEqual("/sync-gate?returnUrl=%2Fhome%2Fpipeline-insights", result.TargetUri);
        Assert.AreEqual(7, preferences.StoredIntValue);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_Ready_UsesServerTargetWithoutClientInference()
    {
        var contractJson = """
            {
              "startupState": 3,
              "targetRoute": "/home/delivery/execution?sprintId=3",
              "returnUrl": "/home/delivery/execution?sprintId=3",
              "activeProfileId": 9,
              "syncStatus": 3,
              "blockedReason": null,
              "diagnostics": {
                "hasSavedTfsConfig": true,
                "hasTestedConnectionSuccessfully": true,
                "hasVerifiedTfsApiSuccessfully": true,
                "hasAnyProfile": true,
                "serverActiveProfilePresent": true,
                "clientHintProvided": true,
                "clientHintApplied": false,
                "clientHintRejected": false,
                "cacheStatePresent": true,
                "syncCompletedSuccessfully": true,
                "syncDataPresent": true,
                "syncAttemptWithinTolerance": true
              }
            }
            """;

        var (profileService, service, preferences, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/startup-state" => CreateJsonResponse(contractJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            },
            storedProfileHintId: 7);

        var result = await service.ResolveStartupStateAsync("/home/delivery/execution?sprintId=3");

        Assert.AreEqual(StartupStateDto.Ready, result.Contract?.StartupState);
        Assert.AreEqual("/home/delivery/execution?sprintId=3", result.TargetUri);
        Assert.IsTrue(result.ShouldRenderCurrentRoute);
        Assert.AreEqual(9, preferences.StoredIntValue);
        Assert.AreEqual(9, profileService.CachedActiveProfileId);
    }

    [TestMethod]
    public async Task ResolveStartupStateAsync_Blocked_UsesServerBlockedRoute()
    {
        var contractJson = """
            {
              "startupState": 4,
              "targetRoute": "/startup-blocked",
              "returnUrl": "/home",
              "activeProfileId": null,
              "syncStatus": 0,
              "blockedReason": 0,
              "diagnostics": {
                "hasSavedTfsConfig": false,
                "hasTestedConnectionSuccessfully": false,
                "hasVerifiedTfsApiSuccessfully": false,
                "hasAnyProfile": false,
                "serverActiveProfilePresent": false,
                "clientHintProvided": false,
                "clientHintApplied": false,
                "clientHintRejected": false,
                "cacheStatePresent": false,
                "syncCompletedSuccessfully": false,
                "syncDataPresent": false,
                "syncAttemptWithinTolerance": false
              }
            }
            """;

        var (_, service, _, _) = CreateSystemUnderTest(
            request => request.RequestUri!.AbsolutePath switch
            {
                "/api/startup-state" => CreateJsonResponse(contractJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            });

        var result = await service.ResolveStartupStateAsync("/");

        Assert.AreEqual(StartupStateDto.Blocked, result.Contract?.StartupState);
        Assert.AreEqual("/startup-blocked", result.TargetUri);
        StringAssert.Contains(result.Reason, "configuration");
    }

    private static (StubProfileService ProfileService, StartupOrchestratorService Service, StubPreferencesService Preferences, HttpClient HttpClient) CreateSystemUnderTest(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        int? storedProfileHintId = null)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var preferencesService = new StubPreferencesService(storedProfileHintId);
        var profileService = new StubProfileService();

        var service = new StartupOrchestratorService(
            new StartupClient(httpClient),
            preferencesService,
            profileService);

        return (profileService, service, preferencesService, httpClient);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
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
        public int? CachedActiveProfileId { get; private set; }

        public Task<IEnumerable<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProfileDto?> GetProfileByIdAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProfileDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProfileDto> CreateProfileAsync(string name, List<int> goalIds, ProfilePictureType pictureType = ProfilePictureType.Default, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProfileDto> UpdateProfileAsync(int id, string name, List<int> goalIds, ProfilePictureType? pictureType = null, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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
