using System.Net.Http;
using PoTool.Client.ApiClient;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

/// <summary>
/// Thin startup adapter that delegates readiness authority to the backend startup-state endpoint.
/// </summary>
public sealed class StartupOrchestratorService : IStartupOrchestratorService
{
    private const string ActiveProfilePreferenceKey = "ActiveProfileId";

    private readonly IStartupClient _startupClient;
    private readonly IPreferencesService _preferencesService;
    private readonly IProfileService _profileService;

    public StartupOrchestratorService(
        IStartupClient startupClient,
        IPreferencesService preferencesService,
        IProfileService profileService)
    {
        _startupClient = startupClient;
        _preferencesService = preferencesService;
        _profileService = profileService;
    }

    public async Task<StartupStateResolution> ResolveStartupStateAsync(
        string? currentRelativeUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profileHintId = await _preferencesService.GetIntAsync(ActiveProfilePreferenceKey);
            var contract = await _startupClient.GetStartupStateAsync(currentRelativeUri, profileHintId, cancellationToken);

            await SynchronizeClientHintAsync(contract);

            return new StartupStateResolution(
                Contract: contract,
                TargetUri: contract.TargetRoute,
                ShouldRenderCurrentRoute: StartupNavigationTargetResolver.IsCurrentTarget(currentRelativeUri, contract.TargetRoute),
                Reason: ResolveReason(contract),
                RecoveryHint: ResolveRecoveryHint(contract));
        }
        catch (HttpRequestException)
        {
            return BuildTransportFailureResolution(
                currentRelativeUri,
                "Startup readiness could not reach the backend service.",
                "Retry after confirming the backend is available.");
        }
        catch (TaskCanceledException)
        {
            return BuildTransportFailureResolution(
                currentRelativeUri,
                "Startup readiness timed out before the backend responded.",
                "Retry after confirming the backend is responsive.");
        }
        catch (ApiException ex) when (GeneratedClientErrorTranslator.IsSuccessfulEmptyResponse(ex))
        {
            return BuildTransportFailureResolution(
                currentRelativeUri,
                "Startup readiness returned an empty response.",
                "Retry startup. If the problem persists, inspect the startup API contract.");
        }
        catch (ApiException ex) when (GeneratedClientErrorTranslator.IsSuccessfulDeserializationFailure(ex))
        {
            return BuildTransportFailureResolution(
                currentRelativeUri,
                "Startup readiness returned an invalid response payload.",
                "Retry startup. If the problem persists, inspect the startup API contract.");
        }
        catch (ApiException ex)
        {
            return BuildTransportFailureResolution(
                currentRelativeUri,
                $"Startup readiness is unavailable (HTTP {ex.StatusCode}).",
                "Retry after confirming the backend is available.");
        }
        catch (Exception)
        {
            return BuildTransportFailureResolution(
                currentRelativeUri,
                "Startup readiness failed unexpectedly.",
                "Retry startup. If the problem persists, inspect the client and API logs.");
        }
    }

    private async Task SynchronizeClientHintAsync(StartupStateResponseDto contract)
    {
        _profileService.SetCachedActiveProfileId(contract.ActiveProfileId);

        if (contract.ActiveProfileId.HasValue)
        {
            await _preferencesService.SetIntAsync(ActiveProfilePreferenceKey, contract.ActiveProfileId.Value);
            return;
        }

        await _preferencesService.RemoveAsync(ActiveProfilePreferenceKey);
    }

    private static StartupStateResolution BuildTransportFailureResolution(
        string? currentRelativeUri,
        string reason,
        string recoveryHint)
    {
        var targetUri = StartupNavigationTargetResolver.GetBlockingRoute();
        return new StartupStateResolution(
            Contract: null,
            TargetUri: targetUri,
            ShouldRenderCurrentRoute: StartupNavigationTargetResolver.IsCurrentTarget(currentRelativeUri, targetUri),
            Reason: reason,
            RecoveryHint: recoveryHint);
    }

    private static string ResolveReason(StartupStateResponseDto contract)
    {
        return contract.StartupState switch
        {
            StartupStateDto.NoProfile => "No active profile is selected.",
            StartupStateDto.ProfileInvalid => "The active profile selection is invalid and must be reselected.",
            StartupStateDto.ProfileValid_NoSync => contract.SyncStatus switch
            {
                StartupSyncStatusDto.InProgress => "Sync is still in progress for the active profile.",
                StartupSyncStatusDto.SuccessWithWarnings => "The latest sync completed with warnings and does not qualify as startup-ready.",
                StartupSyncStatusDto.Failed => "The latest sync failed for the active profile.",
                StartupSyncStatusDto.Invalidated => "A later sync attempt invalidated the last successful startup cache state.",
                StartupSyncStatusDto.MissingData => "The latest successful sync is missing required startup cache data.",
                _ => "The active profile has not completed a successful sync yet."
            },
            StartupStateDto.Blocked => contract.BlockedReason switch
            {
                StartupBlockedReasonDto.MissingConfiguration => "Startup configuration is incomplete.",
                StartupBlockedReasonDto.InvalidActiveProfile => "Startup encountered an invalid persisted active profile.",
                _ => "Startup could not continue safely."
            },
            _ => "Startup checks passed."
        };
    }

    private static string ResolveRecoveryHint(StartupStateResponseDto contract)
    {
        return contract.StartupState switch
        {
            StartupStateDto.NoProfile or StartupStateDto.ProfileInvalid => "Select a valid profile before continuing.",
            StartupStateDto.ProfileValid_NoSync => "Complete a successful sync before entering workspace routes.",
            StartupStateDto.Blocked when contract.BlockedReason == StartupBlockedReasonDto.MissingConfiguration
                => "Open settings and complete the required startup configuration before continuing.",
            StartupStateDto.Blocked => "Retry startup after confirming the backend and configuration are available.",
            _ => "You can continue to the requested route."
        };
    }
}
