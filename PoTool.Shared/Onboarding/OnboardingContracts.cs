namespace PoTool.Shared.Onboarding;

public enum OnboardingErrorCode
{
    ValidationFailed,
    NotFound,
    PermissionDenied,
    TfsUnavailable,
    Conflict,
    DependencyViolation
}

public enum OnboardingValidationStatus
{
    Unknown,
    Valid,
    Invalid,
    Unavailable,
    PermissionDenied,
    CapabilityDenied
}

public enum OnboardingValidationSource
{
    Live,
    SnapshotOnly
}

public enum OnboardingProductSourceTypeDto
{
    Project,
    Team,
    Pipeline
}

public sealed record OnboardingErrorDto(
    OnboardingErrorCode Code,
    string Message,
    string? Details,
    bool Retryable);

public sealed record OnboardingSuccessEnvelope<T>(
    T Data,
    DateTime TimestampUtc);

public sealed class OnboardingOperationResult<T>
{
    private OnboardingOperationResult(T? data, OnboardingErrorDto? error)
    {
        Data = data;
        Error = error;
    }

    public T? Data { get; }

    public OnboardingErrorDto? Error { get; }

    public bool Succeeded => Error is null;

    public static OnboardingOperationResult<T> Success(T data)
        => new(data, null);

    public static OnboardingOperationResult<T> Failure(OnboardingErrorDto error)
        => new(default, error);
}

public sealed record OnboardingValidationStateDto(
    OnboardingValidationStatus Status,
    DateTime CheckedAtUtc,
    OnboardingValidationSource ValidatedFrom,
    string? ErrorCode,
    string? ErrorMessageSanitized,
    IReadOnlyList<string> WarningCodes,
    string? PermissionScopeSummary,
    string? CapabilitySummary,
    string? NotFoundExternalId);

public sealed record SnapshotMetadataDto(
    DateTime ConfirmedAtUtc,
    DateTime LastSeenAtUtc,
    bool IsCurrent,
    bool? RenameDetected,
    string? StaleReason);
