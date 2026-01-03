namespace PoTool.Core.Contracts.TfsVerification;

/// <summary>
/// Categories of TFS API verification failures.
/// </summary>
public enum FailureCategory
{
    /// <summary>
    /// Authentication failed (invalid credentials, expired token).
    /// </summary>
    Authentication,
    
    /// <summary>
    /// Authorization failed (insufficient permissions).
    /// </summary>
    Authorization,
    
    /// <summary>
    /// API endpoint is unavailable or unreachable.
    /// </summary>
    EndpointUnavailable,
    
    /// <summary>
    /// API version not supported by the server.
    /// </summary>
    UnsupportedApiVersion,
    
    /// <summary>
    /// Required field is missing from work item or response.
    /// </summary>
    MissingField,
    
    /// <summary>
    /// Process template does not meet requirements.
    /// </summary>
    InvalidProcessTemplate,
    
    /// <summary>
    /// WIQL query restriction or syntax error.
    /// </summary>
    QueryRestriction,
    
    /// <summary>
    /// Response payload shape does not match expectations.
    /// </summary>
    PayloadShapeMismatch,
    
    /// <summary>
    /// Rate limit exceeded.
    /// </summary>
    RateLimit,
    
    /// <summary>
    /// Unknown or unclassified failure.
    /// </summary>
    Unknown
}
