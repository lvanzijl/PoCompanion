namespace PoTool.Shared.Settings;

/// <summary>
/// Canonical startup state emitted by the authoritative backend startup-state endpoint.
/// </summary>
public enum StartupStateDto
{
    NoProfile,
    ProfileInvalid,
    ProfileValid_NoSync,
    Ready,
    Blocked
}

/// <summary>
/// Explicit startup sync status reported by the backend without client inference.
/// </summary>
public enum StartupSyncStatusDto
{
    NotApplicable,
    Missing,
    InProgress,
    Success,
    SuccessWithWarnings,
    Failed,
    Invalidated,
    MissingData
}

/// <summary>
/// Structured blocked startup categories.
/// </summary>
public enum StartupBlockedReasonDto
{
    MissingConfiguration,
    InvalidActiveProfile,
    UnexpectedFailure
}

/// <summary>
/// Structured diagnostic flags describing the authoritative startup-state evaluation inputs.
/// </summary>
public sealed record StartupDiagnosticFlagsDto(
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
    bool SyncAttemptWithinTolerance);

/// <summary>
/// Versionable startup-state response consumed by the root startup gate.
/// </summary>
public sealed record StartupStateResponseDto(
    StartupStateDto StartupState,
    string TargetRoute,
    string? ReturnUrl,
    int? ActiveProfileId,
    StartupSyncStatusDto SyncStatus,
    StartupBlockedReasonDto? BlockedReason,
    StartupDiagnosticFlagsDto Diagnostics);
