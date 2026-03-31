namespace PoTool.Shared.WorkItems;

/// <summary>
/// DTO representing a suggested fix for a validation violation.
/// </summary>
public sealed record FixValidationViolationDto(
    int WorkItemId,
    string FixType,
    string Description,
    string NewState,
    string Justification
)
{
    public FixValidationViolationDto()
        : this(0, string.Empty, string.Empty, string.Empty, string.Empty)
    {
    }
}

/// <summary>
/// DTO representing the result of batch fix operations.
/// </summary>
public sealed record FixValidationViolationResultDto(
    int TotalAttempted,
    int SuccessfulFixes,
    int FailedFixes,
    IReadOnlyList<FixResult> Results
);

/// <summary>
/// Represents the result of a single fix operation.
/// </summary>
public sealed record FixResult(
    int WorkItemId,
    bool Success,
    string Message
);
