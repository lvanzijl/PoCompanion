namespace PoTool.Shared.Exceptions;

/// <summary>
/// Exception thrown when TFS configuration is missing or incomplete.
/// This is used when the application is configured to use TFS as the data source
/// but the required TFS settings are not properly configured.
/// </summary>
public class TfsConfigurationException : TfsException
{
    /// <summary>
    /// Gets the list of missing or invalid configuration fields.
    /// </summary>
    public IReadOnlyList<string> MissingFields { get; }

    public TfsConfigurationException(string message)
        : base(message)
    {
        MissingFields = Array.Empty<string>();
    }

    public TfsConfigurationException(string message, IEnumerable<string> missingFields)
        : base(message)
    {
        MissingFields = missingFields.ToList().AsReadOnly();
    }

    public TfsConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
        MissingFields = Array.Empty<string>();
    }
}
