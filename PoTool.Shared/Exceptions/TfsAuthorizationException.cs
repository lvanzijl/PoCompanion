namespace PoTool.Shared.Exceptions;

/// <summary>
/// Exception thrown when TFS authorization fails (403 Forbidden).
/// Indicates insufficient permissions for the requested operation.
/// </summary>
public class TfsAuthorizationException : TfsException
{
    public TfsAuthorizationException(string message, string? errorContent)
        : base(message, 403, errorContent)
    {
    }
}
