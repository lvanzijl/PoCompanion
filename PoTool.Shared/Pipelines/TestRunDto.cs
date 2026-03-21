namespace PoTool.Shared.Pipelines;

/// <summary>
/// Raw test run facts linked to a TFS build.
/// </summary>
public record TestRunDto
{
    /// <summary>
    /// TFS build identifier used for linkage to the cached build anchor.
    /// </summary>
    public required int BuildId { get; init; }

    /// <summary>
    /// Stable external test run identifier when exposed by the source API.
    /// </summary>
    public int? ExternalId { get; init; }

    /// <summary>
    /// Total tests reported by the source test run.
    /// </summary>
    public required int TotalTests { get; init; }

    /// <summary>
    /// Passed tests reported by the source test run.
    /// </summary>
    public required int PassedTests { get; init; }

    /// <summary>
    /// Not applicable tests reported by the source test run.
    /// </summary>
    public required int NotApplicableTests { get; init; }

    /// <summary>
    /// Optional source timestamp when the upstream payload exposes a verified run timestamp.
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }
}
