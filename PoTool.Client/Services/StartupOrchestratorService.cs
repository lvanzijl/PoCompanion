using System.Net.Http;
using PoTool.Client.ApiClient;
using SharedStartupReadinessDto = PoTool.Shared.Settings.StartupReadinessDto;

namespace PoTool.Client.Services;

/// <summary>
/// Resolves startup readiness atomically before any route can render.
/// </summary>
public sealed class StartupOrchestratorService : IStartupOrchestratorService
{
    private const string ActiveProfilePreferenceKey = "ActiveProfileId";
    private static readonly TimeSpan SyncAttemptTolerance = TimeSpan.FromSeconds(5);

    private readonly IStartupClient _startupClient;
    private readonly ICacheSyncService _cacheSyncService;
    private readonly IProfileService _profileService;
    private readonly IPreferencesService _preferencesService;

    public StartupOrchestratorService(
        IStartupClient startupClient,
        ICacheSyncService cacheSyncService,
        IProfileService profileService,
        IPreferencesService preferencesService)
    {
        _startupClient = startupClient;
        _cacheSyncService = cacheSyncService;
        _profileService = profileService;
        _preferencesService = preferencesService;
    }

    public async Task<StartupStateResolution> ResolveStartupStateAsync(
        string? currentRelativeUri,
        CancellationToken cancellationToken = default)
    {
        var requestedReadyUri = StartupNavigationTargetResolver.ResolveRequestedReadyUri(currentRelativeUri);

        try
        {
            var readiness = MapReadiness(await _startupClient.GetStartupReadinessAsync(cancellationToken));
            if (RequiresConfiguration(readiness))
            {
                await ClearClientProfileHintAsync();
                return BuildResolution(
                    StartupResolutionState.Blocked,
                    readiness,
                    currentRelativeUri,
                    requestedReadyUri,
                    activeProfileId: null,
                    reason: readiness.MissingRequirementMessage ?? "Startup configuration is incomplete.",
                    recoveryHint: "Open settings and complete the required startup configuration before continuing.",
                    blockedReason: StartupBlockedReason.MissingConfiguration);
            }

            var profileResolution = await ResolveActiveProfileSelectionAsync(readiness, cancellationToken);
            if (profileResolution.State != null)
            {
                return BuildResolution(
                    profileResolution.State.Value,
                    readiness,
                    currentRelativeUri,
                    requestedReadyUri,
                    profileResolution.ActiveProfileId,
                    profileResolution.Reason,
                    profileResolution.RecoveryHint,
                    profileResolution.BlockedReason);
            }

            var cacheState = await _cacheSyncService.GetCacheStatusAsync(profileResolution.ActiveProfileId!.Value, cancellationToken);
            if (!IsStartupSyncValid(cacheState, out var syncReason))
            {
                return BuildResolution(
                    StartupResolutionState.ProfileValid_NoSync,
                    readiness,
                    currentRelativeUri,
                    requestedReadyUri,
                    profileResolution.ActiveProfileId,
                    syncReason,
                    "Complete a successful sync before entering workspace routes.");
            }

            return BuildResolution(
                StartupResolutionState.Ready,
                readiness,
                currentRelativeUri,
                requestedReadyUri,
                profileResolution.ActiveProfileId,
                "Startup checks passed.",
                "You can continue to the requested route.");
        }
        catch (HttpRequestException)
        {
            return BuildResolution(
                StartupResolutionState.Blocked,
                readiness: null,
                currentRelativeUri,
                requestedReadyUri,
                activeProfileId: null,
                reason: "Startup readiness could not reach the backend service.",
                recoveryHint: "Retry after confirming the backend is available.",
                blockedReason: StartupBlockedReason.BackendUnavailable);
        }
        catch (TaskCanceledException)
        {
            return BuildResolution(
                StartupResolutionState.Blocked,
                readiness: null,
                currentRelativeUri,
                requestedReadyUri,
                activeProfileId: null,
                reason: "Startup readiness timed out before the backend responded.",
                recoveryHint: "Retry after confirming the backend is responsive.",
                blockedReason: StartupBlockedReason.BackendUnavailable);
        }
        catch (ApiException ex) when (GeneratedClientErrorTranslator.IsSuccessfulEmptyResponse(ex))
        {
            return BuildResolution(
                StartupResolutionState.Blocked,
                readiness: null,
                currentRelativeUri,
                requestedReadyUri,
                activeProfileId: null,
                reason: "Startup readiness returned an empty response.",
                recoveryHint: "Retry startup. If the problem persists, inspect the startup API contract.",
                blockedReason: StartupBlockedReason.InvalidResponse);
        }
        catch (ApiException ex) when (GeneratedClientErrorTranslator.IsSuccessfulDeserializationFailure(ex))
        {
            return BuildResolution(
                StartupResolutionState.Blocked,
                readiness: null,
                currentRelativeUri,
                requestedReadyUri,
                activeProfileId: null,
                reason: "Startup readiness returned an invalid response payload.",
                recoveryHint: "Retry startup. If the problem persists, inspect the startup API contract.",
                blockedReason: StartupBlockedReason.InvalidResponse);
        }
        catch (ApiException ex)
        {
            return BuildResolution(
                StartupResolutionState.Blocked,
                readiness: null,
                currentRelativeUri,
                requestedReadyUri,
                activeProfileId: null,
                reason: $"Startup readiness is unavailable (HTTP {ex.StatusCode}).",
                recoveryHint: "Retry after confirming the backend is available.",
                blockedReason: StartupBlockedReason.BackendUnavailable);
        }
        catch (Exception)
        {
            return BuildResolution(
                StartupResolutionState.Blocked,
                readiness: null,
                currentRelativeUri,
                requestedReadyUri,
                activeProfileId: null,
                reason: "Startup readiness failed unexpectedly.",
                recoveryHint: "Retry startup. If the problem persists, inspect the client and API logs.",
                blockedReason: StartupBlockedReason.UnexpectedFailure);
        }
    }

    private async Task<ProfileSelectionResolution> ResolveActiveProfileSelectionAsync(
        StartupReadinessDto readiness,
        CancellationToken cancellationToken)
    {
        if (!readiness.HasAnyProfile)
        {
            await ClearClientProfileHintAsync();
            _profileService.SetCachedActiveProfileId(null);
            return ProfileSelectionResolution.NoSelection(
                StartupResolutionState.NoProfile,
                "No active profile is available yet.",
                "Create or select a profile before continuing.");
        }

        var cachedProfileId = await _preferencesService.GetIntAsync(ActiveProfilePreferenceKey);
        if (readiness.ActiveProfileId.HasValue)
        {
            var serverProfile = await _profileService.GetProfileByIdAsync(readiness.ActiveProfileId.Value, cancellationToken);
            if (serverProfile == null)
            {
                await ClearServerActiveProfileAsync(cancellationToken);
                await ClearClientProfileHintAsync();
                return ProfileSelectionResolution.NoSelection(
                    StartupResolutionState.ProfileInvalid,
                    "The server references an active profile that no longer exists.",
                    "Select a valid profile before continuing.");
            }

            await PersistClientProfileHintAsync(serverProfile.Id);
            _profileService.SetCachedActiveProfileId(serverProfile.Id);
            return ProfileSelectionResolution.Selected(serverProfile.Id);
        }

        if (!cachedProfileId.HasValue)
        {
            _profileService.SetCachedActiveProfileId(null);
            return ProfileSelectionResolution.NoSelection(
                StartupResolutionState.NoProfile,
                "No active profile is selected.",
                "Select a profile before continuing.");
        }

        var hintedProfile = await _profileService.GetProfileByIdAsync(cachedProfileId.Value, cancellationToken);
        if (hintedProfile == null)
        {
            await ClearClientProfileHintAsync();
            _profileService.SetCachedActiveProfileId(null);
            return ProfileSelectionResolution.NoSelection(
                StartupResolutionState.ProfileInvalid,
                "The cached browser profile selection is no longer valid.",
                "Select a valid profile before continuing.");
        }

        await _profileService.SetActiveProfileAsync(hintedProfile.Id, cancellationToken);
        await PersistClientProfileHintAsync(hintedProfile.Id);
        _profileService.SetCachedActiveProfileId(hintedProfile.Id);
        return ProfileSelectionResolution.Selected(hintedProfile.Id);
    }

    private static bool IsStartupSyncValid(CacheStateDto? cacheState, out string reason)
    {
        if (cacheState == null)
        {
            reason = "Sync state could not be determined for the active profile.";
            return false;
        }

        if (cacheState.SyncStatus != CacheSyncStatusDto.Success)
        {
            reason = cacheState.SyncStatus switch
            {
                CacheSyncStatusDto.InProgress => "Sync is still in progress for the active profile.",
                CacheSyncStatusDto.SuccessWithWarnings => "The latest sync completed with warnings and does not qualify as startup-ready.",
                CacheSyncStatusDto.Failed => cacheState.LastErrorMessage ?? "The latest sync failed for the active profile.",
                _ => "The active profile has not completed a successful sync yet."
            };
            return false;
        }

        if (!cacheState.LastSuccessfulSync.HasValue)
        {
            reason = "The active profile has no completed successful sync timestamp.";
            return false;
        }

        if (!cacheState.LastAttemptSync.HasValue)
        {
            reason = "The active profile has no recorded sync attempt timestamp.";
            return false;
        }

        if (cacheState.LastAttemptSync.Value > cacheState.LastSuccessfulSync.Value.Add(SyncAttemptTolerance))
        {
            reason = "A later sync attempt has invalidated the last successful startup cache state.";
            return false;
        }

        if (!cacheState.WorkItemWatermark.HasValue)
        {
            reason = "The latest successful sync did not produce the required work item watermark.";
            return false;
        }

        reason = "Startup sync is valid.";
        return true;
    }

    private async Task PersistClientProfileHintAsync(int profileId)
    {
        await _preferencesService.SetIntAsync(ActiveProfilePreferenceKey, profileId);
    }

    private async Task ClearClientProfileHintAsync()
    {
        await _preferencesService.RemoveAsync(ActiveProfilePreferenceKey);
    }

    private async Task ClearServerActiveProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _profileService.SetActiveProfileAsync(null, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Startup will continue in a blocked or invalid state even if cleanup fails.
        }
        catch (ApiException)
        {
            // Startup will continue in a blocked or invalid state even if cleanup fails.
        }
    }

    private static bool RequiresConfiguration(StartupReadinessDto readiness)
    {
        return !readiness.HasSavedTfsConfig
               || !readiness.HasTestedConnectionSuccessfully
               || !readiness.HasVerifiedTfsApiSuccessfully;
    }

    private static StartupStateResolution BuildResolution(
        StartupResolutionState state,
        StartupReadinessDto? readiness,
        string? currentRelativeUri,
        string requestedReadyUri,
        int? activeProfileId,
        string reason,
        string recoveryHint,
        StartupBlockedReason? blockedReason = null)
    {
        var targetUri = StartupNavigationTargetResolver.GetTargetUri(state, requestedReadyUri, reason, recoveryHint);
        var shouldRenderCurrentRoute = StartupNavigationTargetResolver.IsCurrentTarget(currentRelativeUri, targetUri);
        return new StartupStateResolution(
            state,
            readiness,
            requestedReadyUri,
            targetUri,
            shouldRenderCurrentRoute,
            activeProfileId,
            reason,
            recoveryHint,
            blockedReason);
    }

    private static StartupReadinessDto MapReadiness(SharedStartupReadinessDto readiness)
    {
        return new StartupReadinessDto(
            readiness.IsMockDataEnabled,
            readiness.HasSavedTfsConfig,
            readiness.HasTestedConnectionSuccessfully,
            readiness.HasVerifiedTfsApiSuccessfully,
            readiness.HasAnyProfile,
            readiness.ActiveProfileId,
            readiness.MissingRequirementMessage);
    }

    private sealed record ProfileSelectionResolution(
        int? ActiveProfileId,
        StartupResolutionState? State,
        string Reason,
        string RecoveryHint,
        StartupBlockedReason? BlockedReason = null)
    {
        public static ProfileSelectionResolution Selected(int profileId)
            => new(profileId, null, string.Empty, string.Empty);

        public static ProfileSelectionResolution NoSelection(
            StartupResolutionState state,
            string reason,
            string recoveryHint)
            => new(null, state, reason, recoveryHint);
    }
}
