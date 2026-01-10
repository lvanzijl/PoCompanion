namespace PoTool.Shared.Contracts.TfsVerification;

/// <summary>
/// Result of a single TFS capability verification check.
/// </summary>
public class TfsCapabilityCheckResult
{
    /// <summary>
    /// Stable identifier for this capability check.
    /// </summary>
    public required string CapabilityId { get; init; }

    /// <summary>
    /// Whether the capability check passed.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Description of which user-visible functionality is impacted by this capability.
    /// </summary>
    public required string ImpactedFunctionality { get; init; }

    /// <summary>
    /// What the tool expects the TFS server to support.
    /// </summary>
    public required string ExpectedBehavior { get; init; }

    /// <summary>
    /// What actually happened during verification.
    /// </summary>
    public string? ObservedBehavior { get; init; }

    /// <summary>
    /// Category of failure (only present if Success is false).
    /// </summary>
    public FailureCategory? FailureCategory { get; init; }

    /// <summary>
    /// Sanitized evidence from the failure (HTTP status, error codes, truncated messages).
    /// Must not contain secrets, tokens, or headers.
    /// </summary>
    public string? RawEvidence { get; init; }

    /// <summary>
    /// Ordered list of likely causes for the failure.
    /// </summary>
    public List<string>? LikelyCauses { get; init; }

    /// <summary>
    /// Concrete steps to resolve the failure.
    /// </summary>
    public List<string>? ResolutionGuidance { get; init; }

    /// <summary>
    /// For write checks: description of which work item(s) were affected.
    /// </summary>
    public string? TargetScope { get; init; }

    /// <summary>
    /// For write checks: type of mutation performed.
    /// </summary>
    public MutationType? MutationType { get; init; }

    /// <summary>
    /// For write checks: cleanup status.
    /// </summary>
    public CleanupStatus? CleanupStatus { get; init; }
}
