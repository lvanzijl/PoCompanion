namespace PoTool.Client.Services;

/// <summary>
/// Implementation of correlation ID management for request tracking.
/// </summary>
public class CorrelationIdService : ICorrelationIdService
{
    private string? _correlationId;

    /// <inheritdoc/>
    public string GetOrCreateCorrelationId()
    {
        if (string.IsNullOrEmpty(_correlationId))
        {
            _correlationId = Guid.NewGuid().ToString("N");
            Console.WriteLine($"[CorrelationId] Generated new correlation ID: {_correlationId}");
        }
        return _correlationId;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _correlationId = null;
    }
}
