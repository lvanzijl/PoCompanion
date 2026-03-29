namespace PoTool.Api.Exceptions;

/// <summary>
/// Thrown when a request attempts to use a provider that is incompatible with the resolved data source mode.
/// </summary>
public sealed class InvalidDataSourceUsageException : InvalidOperationException
{
    public InvalidDataSourceUsageException(
        string? path,
        string mode,
        string attemptedProvider)
        : base($"Route '{path ?? "<unknown>"}' resolved to mode '{mode}' but attempted provider '{attemptedProvider}'.")
    {
        Path = path;
        Mode = mode;
        AttemptedProvider = attemptedProvider;
    }

    public string? Path { get; }

    public string Mode { get; }

    public string AttemptedProvider { get; }
}
