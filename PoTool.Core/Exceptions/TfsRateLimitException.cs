namespace PoTool.Core.Exceptions;

/// <summary>
/// Exception thrown when TFS rate limiting is triggered (429 Too Many Requests).
/// Indicates the client should back off and retry later.
/// </summary>
public class TfsRateLimitException : TfsException
{
    /// <summary>
    /// Time to wait before retrying, if provided by TFS.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public TfsRateLimitException(string message, string? errorContent, TimeSpan? retryAfter = null) 
        : base(message, 429, errorContent)
    {
        RetryAfter = retryAfter;
    }
}
