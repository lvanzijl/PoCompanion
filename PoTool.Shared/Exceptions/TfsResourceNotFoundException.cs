namespace PoTool.Shared.Exceptions;

/// <summary>
/// Exception thrown when a TFS resource is not found (404 Not Found).
/// Indicates the project, repository, or work item does not exist.
/// </summary>
public class TfsResourceNotFoundException : TfsException
{
    public TfsResourceNotFoundException(string message, string? errorContent) 
        : base(message, 404, errorContent)
    {
    }
}
