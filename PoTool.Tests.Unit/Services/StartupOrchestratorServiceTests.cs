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
    private StartupOrchestratorService CreateService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost")
        };

        return new StartupOrchestratorService(
            new StartupClient(httpClient),
            new CacheSyncService(httpClient, new CacheSyncClient(httpClient)));
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
                "/api/startup/readiness" => CreateJsonResponse(readinessJson),
                "/api/CacheSync/7" => CreateJsonResponse(cacheJson),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            };
        });

        var result = await service.GetStartupReadinessAsync();

        Assert.AreEqual(StartupReadinessState.SyncRequired, result.State);
        Assert.IsNotNull(result.Readiness);
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
}
