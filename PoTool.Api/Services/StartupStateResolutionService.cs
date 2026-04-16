using System.Web;
using PoTool.Api.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Resolves the single authoritative startup-state contract used by the root startup gate.
/// </summary>
public sealed class StartupStateResolutionService
{
    private static readonly HashSet<string> StartupFlowPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/profiles",
        "/sync-gate",
        "/startup-blocked"
    };

    private static readonly TimeSpan SyncAttemptTolerance = TimeSpan.FromSeconds(5);

    private readonly TfsConfigurationService _tfsConfigService;
    private readonly IProfileRepository _profileRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ICacheStateRepository _cacheStateRepository;
    private readonly TfsRuntimeMode _runtimeMode;

    public StartupStateResolutionService(
        TfsConfigurationService tfsConfigService,
        IProfileRepository profileRepository,
        ISettingsRepository settingsRepository,
        ICacheStateRepository cacheStateRepository,
        TfsRuntimeMode runtimeMode)
    {
        _tfsConfigService = tfsConfigService;
        _profileRepository = profileRepository;
        _settingsRepository = settingsRepository;
        _cacheStateRepository = cacheStateRepository;
        _runtimeMode = runtimeMode;
    }

    public async Task<StartupStateResponseDto> ResolveAsync(
        string? returnUrl,
        int? profileHintId,
        CancellationToken cancellationToken = default)
    {
        var normalizedReturnUrl = ResolveRequestedRoute(returnUrl);

        var tfsConfig = await _tfsConfigService.GetConfigEntityAsync(cancellationToken);
        var hasSavedTfsConfig = tfsConfig != null && !string.IsNullOrWhiteSpace(tfsConfig.Url);
        var hasTestedConnectionSuccessfully = tfsConfig?.HasTestedConnectionSuccessfully ?? false;
        var hasVerifiedTfsApiSuccessfully = tfsConfig?.HasVerifiedTfsApiSuccessfully ?? false;
        var requiresConfiguration = _runtimeMode.IsRealDataMode
            && (!hasSavedTfsConfig || !hasTestedConnectionSuccessfully || !hasVerifiedTfsApiSuccessfully);
        var hasAnyProfile = await _profileRepository.HasAnyProfileAsync(cancellationToken);

        var diagnostics = new StartupDiagnosticFlags(
            HasSavedTfsConfig: hasSavedTfsConfig,
            HasTestedConnectionSuccessfully: hasTestedConnectionSuccessfully,
            HasVerifiedTfsApiSuccessfully: hasVerifiedTfsApiSuccessfully,
            HasAnyProfile: hasAnyProfile,
            ServerActiveProfilePresent: false,
            ClientHintProvided: profileHintId.HasValue,
            ClientHintApplied: false,
            ClientHintRejected: false,
            CacheStatePresent: false,
            SyncCompletedSuccessfully: false,
            SyncDataPresent: false,
            SyncAttemptWithinTolerance: false);

        if (requiresConfiguration)
        {
            return BuildResponse(
                StartupStateDto.Blocked,
                "/startup-blocked",
                normalizedReturnUrl,
                activeProfileId: null,
                StartupSyncStatusDto.NotApplicable,
                StartupBlockedReasonDto.MissingConfiguration,
                diagnostics);
        }

        if (!hasAnyProfile)
        {
            if (profileHintId.HasValue)
            {
                diagnostics = diagnostics with { ClientHintRejected = true };
            }

            return BuildResponse(
                StartupStateDto.NoProfile,
                BuildProfilesRoute(normalizedReturnUrl),
                normalizedReturnUrl,
                activeProfileId: null,
                StartupSyncStatusDto.NotApplicable,
                blockedReason: null,
                diagnostics);
        }

        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
        var persistedActiveProfileId = settings?.ActiveProfileId;
        diagnostics = diagnostics with { ServerActiveProfilePresent = persistedActiveProfileId.HasValue };

        if (persistedActiveProfileId.HasValue)
        {
            var persistedProfile = await _profileRepository.GetProfileByIdAsync(persistedActiveProfileId.Value, cancellationToken);
            if (persistedProfile == null)
            {
                await _settingsRepository.SetActiveProfileAsync(null, cancellationToken);
                diagnostics = diagnostics with
                {
                    ServerActiveProfilePresent = false,
                    ClientHintRejected = profileHintId.HasValue
                };

                return BuildResponse(
                    StartupStateDto.ProfileInvalid,
                    BuildProfilesRoute(normalizedReturnUrl),
                    normalizedReturnUrl,
                    activeProfileId: null,
                    StartupSyncStatusDto.NotApplicable,
                    blockedReason: null,
                    diagnostics);
            }

            diagnostics = diagnostics with
            {
                ClientHintRejected = profileHintId.HasValue && profileHintId.Value != persistedProfile.Id
            };

            return await BuildProfileResolvedResponseAsync(
                persistedProfile.Id,
                normalizedReturnUrl,
                diagnostics,
                cancellationToken);
        }

        if (!profileHintId.HasValue)
        {
            return BuildResponse(
                StartupStateDto.NoProfile,
                BuildProfilesRoute(normalizedReturnUrl),
                normalizedReturnUrl,
                activeProfileId: null,
                StartupSyncStatusDto.NotApplicable,
                blockedReason: null,
                diagnostics);
        }

        var hintedProfile = await _profileRepository.GetProfileByIdAsync(profileHintId.Value, cancellationToken);
        if (hintedProfile == null)
        {
            diagnostics = diagnostics with { ClientHintRejected = true };
            return BuildResponse(
                StartupStateDto.ProfileInvalid,
                BuildProfilesRoute(normalizedReturnUrl),
                normalizedReturnUrl,
                activeProfileId: null,
                StartupSyncStatusDto.NotApplicable,
                blockedReason: null,
                diagnostics);
        }

        await _settingsRepository.SetActiveProfileAsync(hintedProfile.Id, cancellationToken);
        diagnostics = diagnostics with
        {
            ServerActiveProfilePresent = true,
            ClientHintApplied = true
        };

        return await BuildProfileResolvedResponseAsync(
            hintedProfile.Id,
            normalizedReturnUrl,
            diagnostics,
            cancellationToken);
    }

    private async Task<StartupStateResponseDto> BuildProfileResolvedResponseAsync(
        int activeProfileId,
        string normalizedReturnUrl,
        StartupDiagnosticFlags diagnostics,
        CancellationToken cancellationToken)
    {
        var cacheState = await _cacheStateRepository.GetCacheStateAsync(activeProfileId, cancellationToken);
        diagnostics = diagnostics with { CacheStatePresent = cacheState != null };

        var (syncStatus, syncDataPresent, syncAttemptWithinTolerance) = EvaluateSyncStatus(cacheState);
        diagnostics = diagnostics with
        {
            SyncCompletedSuccessfully = syncStatus == StartupSyncStatusDto.Success,
            SyncDataPresent = syncDataPresent,
            SyncAttemptWithinTolerance = syncAttemptWithinTolerance
        };

        if (syncStatus != StartupSyncStatusDto.Success)
        {
            return BuildResponse(
                StartupStateDto.ProfileValid_NoSync,
                BuildSyncGateRoute(normalizedReturnUrl),
                normalizedReturnUrl,
                activeProfileId,
                syncStatus,
                blockedReason: null,
                diagnostics);
        }

        return BuildResponse(
            StartupStateDto.Ready,
            normalizedReturnUrl,
            normalizedReturnUrl,
            activeProfileId,
            StartupSyncStatusDto.Success,
            blockedReason: null,
            diagnostics);
    }

    private (StartupSyncStatusDto SyncStatus, bool SyncDataPresent, bool SyncAttemptWithinTolerance) EvaluateSyncStatus(CacheStateDto? cacheState)
    {
        if (cacheState == null)
        {
            return (StartupSyncStatusDto.Missing, false, false);
        }

        if (cacheState.SyncStatus != CacheSyncStatusDto.Success)
        {
            return cacheState.SyncStatus switch
            {
                CacheSyncStatusDto.InProgress => (StartupSyncStatusDto.InProgress, false, false),
                CacheSyncStatusDto.SuccessWithWarnings => (StartupSyncStatusDto.SuccessWithWarnings, HasSyncData(cacheState), IsSyncAttemptWithinTolerance(cacheState)),
                CacheSyncStatusDto.Failed => (StartupSyncStatusDto.Failed, HasSyncData(cacheState), IsSyncAttemptWithinTolerance(cacheState)),
                _ => (StartupSyncStatusDto.Missing, false, false)
            };
        }

        if (!cacheState.LastSuccessfulSync.HasValue || !cacheState.LastAttemptSync.HasValue)
        {
            return (StartupSyncStatusDto.MissingData, false, false);
        }

        var withinTolerance = IsSyncAttemptWithinTolerance(cacheState);
        if (!withinTolerance)
        {
            return (StartupSyncStatusDto.Invalidated, HasSyncData(cacheState), false);
        }

        if (!HasSyncData(cacheState))
        {
            return (StartupSyncStatusDto.MissingData, false, true);
        }

        return (StartupSyncStatusDto.Success, true, true);
    }

    private static bool HasSyncData(CacheStateDto cacheState)
    {
        return cacheState.LastSuccessfulSync.HasValue
               && cacheState.LastAttemptSync.HasValue
               && cacheState.WorkItemWatermark.HasValue;
    }

    private static bool IsSyncAttemptWithinTolerance(CacheStateDto cacheState)
    {
        return cacheState.LastSuccessfulSync.HasValue
               && cacheState.LastAttemptSync.HasValue
               && cacheState.LastAttemptSync.Value <= cacheState.LastSuccessfulSync.Value.Add(SyncAttemptTolerance);
    }

    private static StartupStateResponseDto BuildResponse(
        StartupStateDto startupState,
        string targetRoute,
        string? returnUrl,
        int? activeProfileId,
        StartupSyncStatusDto syncStatus,
        StartupBlockedReasonDto? blockedReason,
        StartupDiagnosticFlags diagnostics)
    {
        return new StartupStateResponseDto(
            startupState,
            targetRoute,
            returnUrl,
            activeProfileId,
            syncStatus,
            blockedReason,
            diagnostics.ToDto());
    }

    private static string BuildProfilesRoute(string normalizedReturnUrl)
        => $"/profiles?returnUrl={Uri.EscapeDataString(normalizedReturnUrl)}";

    private static string BuildSyncGateRoute(string normalizedReturnUrl)
        => $"/sync-gate?returnUrl={Uri.EscapeDataString(normalizedReturnUrl)}";

    private static string ResolveRequestedRoute(string? rawRoute)
    {
        var relativeUri = string.IsNullOrWhiteSpace(rawRoute)
            ? "/"
            : rawRoute;
        var absoluteUri = new Uri($"https://startup.local/{relativeUri.TrimStart('/')}");
        var currentPath = NormalizePath(absoluteUri.AbsolutePath);

        if (StartupFlowPaths.Contains(currentPath))
        {
            var query = HttpUtility.ParseQueryString(absoluteUri.Query);
            if (TryNormalizeReturnUrl(query["returnUrl"], out var nestedReturnUrl))
            {
                return nestedReturnUrl;
            }

            return currentPath.Equals("/profiles", StringComparison.OrdinalIgnoreCase)
                ? "/profiles"
                : "/home";
        }

        return TryNormalizeReturnUrl($"{currentPath}{absoluteUri.Query}", out var normalizedReturnUrl)
            ? normalizedReturnUrl
            : "/home";
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : normalized;
    }

    private static bool TryNormalizeReturnUrl(string? returnUrl, out string normalizedReturnUrl)
    {
        normalizedReturnUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        var candidate = returnUrl.Trim();
        if (!candidate.StartsWith("/", StringComparison.Ordinal)
            || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.IndexOf("//", 2, StringComparison.Ordinal) >= 0
            || candidate.Contains('\\')
            || candidate.Contains(':')
            || !Uri.TryCreate(candidate, UriKind.Relative, out _))
        {
            return false;
        }

        normalizedReturnUrl = candidate;
        return true;
    }

    private sealed record StartupDiagnosticFlags(
        bool HasSavedTfsConfig,
        bool HasTestedConnectionSuccessfully,
        bool HasVerifiedTfsApiSuccessfully,
        bool HasAnyProfile,
        bool ServerActiveProfilePresent,
        bool ClientHintProvided,
        bool ClientHintApplied,
        bool ClientHintRejected,
        bool CacheStatePresent,
        bool SyncCompletedSuccessfully,
        bool SyncDataPresent,
        bool SyncAttemptWithinTolerance)
    {
        public StartupDiagnosticFlagsDto ToDto()
            => new(
                HasSavedTfsConfig,
                HasTestedConnectionSuccessfully,
                HasVerifiedTfsApiSuccessfully,
                HasAnyProfile,
                ServerActiveProfilePresent,
                ClientHintProvided,
                ClientHintApplied,
                ClientHintRejected,
                CacheStatePresent,
                SyncCompletedSuccessfully,
                SyncDataPresent,
                SyncAttemptWithinTolerance);
    }
}
