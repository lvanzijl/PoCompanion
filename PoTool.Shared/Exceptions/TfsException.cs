namespace PoTool.Shared.Exceptions;

/// <summary>
/// Base exception for TFS/Azure DevOps integration errors.
/// </summary>
public class TfsException : Exception
{
    /// <summary>
    /// HTTP status code associated with the error, if applicable.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Raw error content from TFS response.
    /// </summary>
    public string? ErrorContent { get; }

    public TfsException(string message) : base(message)
    {
    }

    public TfsException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public TfsException(string message, string? errorContent) : base(message)
    {
        ErrorContent = errorContent;
    }

    public TfsException(string message, int statusCode, string? errorContent) : base(message)
    {
        StatusCode = statusCode;
        ErrorContent = errorContent;
    }
}
