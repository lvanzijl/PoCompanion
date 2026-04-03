using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

/// <summary>
/// Service that orchestrates startup routing based on TFS configuration and profile state.
/// Implements the decision tree from User_landing_v2.md.
/// </summary>
public class StartupOrchestratorService : IStartupOrchestratorService
{
    private readonly HttpClient _httpClient;
    private readonly CacheSyncService _cacheSyncService;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public StartupOrchestratorService(HttpClient httpClient, CacheSyncService cacheSyncService)
    {
        _httpClient = httpClient;
        _cacheSyncService = cacheSyncService;
    }

    /// <inheritdoc />
    public async Task<StartupReadinessResult> GetStartupReadinessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/startup/readiness", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new StartupReadinessResult(
                    StartupReadinessState.Unavailable,
                    Readiness: null,
                    Reason: $"Startup readiness is unavailable (HTTP {(int)response.StatusCode}).",
                    RecoveryHint: "Confirm the backend is running, then retry startup.");
            }

            var readiness = await response.Content.ReadFromJsonAsync<StartupReadinessDto>(_jsonOptions, cancellationToken);
            if (readiness == null)
            {
                return new StartupReadinessResult(
                    StartupReadinessState.Error,
                    Readiness: null,
                    Reason: "Startup readiness returned an empty response.",
                    RecoveryHint: "Retry startup. If the problem persists, check the API response contract.");
            }

            return await ClassifyReadinessAsync(readiness, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new StartupReadinessResult(
                StartupReadinessState.Unavailable,
                Readiness: null,
                Reason: "Startup readiness could not reach the backend service.",
                RecoveryHint: "Start the backend or verify the configured API base URL, then retry.");
        }
        catch (TaskCanceledException)
        {
            return new StartupReadinessResult(
                StartupReadinessState.Unavailable,
                Readiness: null,
                Reason: "Startup readiness timed out before the backend responded.",
                RecoveryHint: "Retry startup after confirming the backend is responsive.");
        }
        catch (JsonException)
        {
            return new StartupReadinessResult(
                StartupReadinessState.Error,
                Readiness: null,
                Reason: "Startup readiness returned an invalid response payload.",
                RecoveryHint: "Check the backend startup endpoint contract, then retry.");
        }
        catch (Exception)
        {
            return new StartupReadinessResult(
                StartupReadinessState.Error,
                Readiness: null,
                Reason: "Startup readiness failed unexpectedly.",
                RecoveryHint: "Retry startup. If the problem persists, inspect the client and API logs.");
        }
    }

    /// <inheritdoc />
    public StartupRoutingResult DetermineRoute(StartupReadinessResult readiness)
    {
        return readiness.State switch
        {
            StartupReadinessState.Ready => new StartupRoutingResult(
                StartupRoute.Home,
                readiness.Reason,
                readiness.RecoveryHint,
                IsBlocking: false),

            StartupReadinessState.NotReady => new StartupRoutingResult(
                StartupRoute.ProfilesHome,
                readiness.Reason,
                readiness.RecoveryHint,
                IsBlocking: false),

            StartupReadinessState.SetupRequired when readiness.Readiness is { HasSavedTfsConfig: false }
                or { HasTestedConnectionSuccessfully: false }
                or { HasVerifiedTfsApiSuccessfully: false } => new StartupRoutingResult(
                    StartupRoute.Configuration,
                    readiness.Reason,
                    readiness.RecoveryHint,
                    IsBlocking: false),

            StartupReadinessState.SetupRequired => new StartupRoutingResult(
                StartupRoute.CreateFirstProfile,
                readiness.Reason,
                readiness.RecoveryHint,
                IsBlocking: false),

            StartupReadinessState.SyncRequired => new StartupRoutingResult(
                StartupRoute.SyncGate,
                readiness.Reason,
                readiness.RecoveryHint,
                IsBlocking: false),

            StartupReadinessState.Unavailable or StartupReadinessState.Error => new StartupRoutingResult(
                StartupRoute.BlockingError,
                readiness.Reason,
                readiness.RecoveryHint,
                IsBlocking: true),

            _ => new StartupRoutingResult(
                StartupRoute.BlockingError,
                "Startup readiness is in an unknown state.",
                "Retry startup after confirming the backend is available.",
                IsBlocking: true)
        };
    }

    /// <inheritdoc />
    public bool IsFeaturePageAccessible(StartupReadinessResult readiness)
    {
        return readiness.State == StartupReadinessState.Ready;
    }

    private async Task<StartupReadinessResult> ClassifyReadinessAsync(
        StartupReadinessDto readiness,
        CancellationToken cancellationToken)
    {
        if (RequiresSetup(readiness))
        {
            return new StartupReadinessResult(
                StartupReadinessState.SetupRequired,
                readiness,
                readiness.MissingRequirementMessage ?? "Setup must be completed before continuing.",
                BuildSetupRecoveryHint(readiness));
        }

        if (readiness.ActiveProfileId == null)
        {
            return new StartupReadinessResult(
                StartupReadinessState.NotReady,
                readiness,
                readiness.MissingRequirementMessage ?? "An active profile must be selected before continuing.",
                "Open Profiles and select the profile you want to use.");
        }

        var cacheState = await _cacheSyncService.GetCacheStatusAsync(readiness.ActiveProfileId.Value, cancellationToken);
        if (cacheState == null)
        {
            return new StartupReadinessResult(
                StartupReadinessState.Unavailable,
                readiness,
                "Cache readiness could not be determined for the active profile.",
                "Open Sync Gate after confirming the backend is available, then retry.");
        }

        if (!cacheState.LastSuccessfulSync.HasValue)
        {
            return new StartupReadinessResult(
                StartupReadinessState.SyncRequired,
                readiness,
                cacheState.LastErrorMessage ?? "The active profile must complete a cache sync before workspace pages can load.",
                "Open Sync Gate to build or refresh the cache for the active profile.");
        }

        return new StartupReadinessResult(
            StartupReadinessState.Ready,
            readiness,
            "Startup checks passed.",
            "You can continue to the workspace.");
    }

    private static bool RequiresSetup(StartupReadinessDto readiness)
    {
        return !readiness.HasSavedTfsConfig
               || !readiness.HasTestedConnectionSuccessfully
               || !readiness.HasVerifiedTfsApiSuccessfully
               || !readiness.HasAnyProfile;
    }

    private static string BuildSetupRecoveryHint(StartupReadinessDto readiness)
    {
        if (!readiness.HasSavedTfsConfig || !readiness.HasTestedConnectionSuccessfully || !readiness.HasVerifiedTfsApiSuccessfully)
        {
            return "Open TFS settings and complete the required connection and verification steps.";
        }

        return "Create your first profile to continue.";
    }
}
