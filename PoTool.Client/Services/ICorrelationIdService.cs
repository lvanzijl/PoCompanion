namespace PoTool.Client.Services;

/// <summary>
/// Service for managing correlation IDs for request tracking.
/// </summary>
public interface ICorrelationIdService
{
    /// <summary>
    /// Gets or generates a correlation ID for the current request context.
    /// </summary>
    string GetOrCreateCorrelationId();

    /// <summary>
    /// Resets the current correlation ID, forcing generation of a new one.
    /// </summary>
    void Reset();
}
