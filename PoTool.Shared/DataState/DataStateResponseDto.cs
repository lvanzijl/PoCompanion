namespace PoTool.Shared.DataState;

/// <summary>
/// Explicit data-availability states for cache-backed reads.
/// </summary>
public enum DataStateDto
{
    NotRequested = 0,
    Loading = 1,
    Available = 2,
    Empty = 3,
    NotReady = 4,
    Failed = 5
}

/// <summary>
/// Structured response wrapper for cache-backed reads so cache readiness is expressed as data.
/// </summary>
public sealed record DataStateResponseDto<T>
{
    public required DataStateDto State { get; init; }

    public T? Data { get; init; }

    public string? Reason { get; init; }

    public int? RetryAfterSeconds { get; init; }
}
