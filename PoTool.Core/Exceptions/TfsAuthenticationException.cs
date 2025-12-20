namespace PoTool.Core.Exceptions;

/// <summary>
/// Exception thrown when TFS authentication fails (401 Unauthorized).
/// Indicates invalid or expired credentials.
/// </summary>
public class TfsAuthenticationException : TfsException
{
    public TfsAuthenticationException(string message, string? errorContent) 
        : base(message, 401, errorContent)
    {
    }

    public TfsAuthenticationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
