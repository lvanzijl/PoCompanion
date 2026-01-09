namespace PoTool.Shared.Contracts.TfsVerification;

/// <summary>
/// Complete report of TFS API capability verification.
/// </summary>
public class TfsVerificationReport
{
    /// <summary>
    /// When the verification was performed.
    /// </summary>
    public required DateTimeOffset VerifiedAt { get; init; }
    
    /// <summary>
    /// TFS server URL that was verified.
    /// </summary>
    public required string ServerUrl { get; init; }
    
    /// <summary>
    /// Project name that was verified.
    /// </summary>
    public required string ProjectName { get; init; }
    
    /// <summary>
    /// API version that was tested.
    /// </summary>
    public required string ApiVersion { get; init; }
    
    /// <summary>
    /// Whether write checks were included.
    /// </summary>
    public required bool IncludedWriteChecks { get; init; }
    
    /// <summary>
    /// Overall success - true if all checks passed.
    /// </summary>
    public required bool Success { get; init; }
    
    /// <summary>
    /// Individual capability check results.
    /// </summary>
    public required List<TfsCapabilityCheckResult> Checks { get; init; }
    
    /// <summary>
    /// Summary of verification results.
    /// </summary>
    public string Summary => $"{Checks.Count(c => c.Success)}/{Checks.Count} checks passed";
    
    /// <summary>
    /// List of all impacted functionalities (from failed checks).
    /// </summary>
    public List<string> ImpactedFunctionalities => 
        Checks.Where(c => !c.Success)
              .Select(c => c.ImpactedFunctionality)
              .Distinct()
              .ToList();
}
