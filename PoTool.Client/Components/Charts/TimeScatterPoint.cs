namespace PoTool.Client.Components.Charts;

/// <summary>
/// View-model for a single data point in the TimeScatterSvg chart.
///
/// X    = build start time within the sprint
/// Y    = build duration in minutes
/// Category = pipeline result (Succeeded / Failed / PartiallySucceeded / Canceled)
/// Label    = build number / display name (shown in tooltip)
/// Metadata = extended fields for the tooltip and Build Summary drawer
///            Keys: PipelineName, FinishTime, Branch, Url, PipelineDefinitionId
/// </summary>
public sealed class TimeScatterPoint
{
    /// <summary>Unique identifier (matches CachedPipelineRun.Id).</summary>
    public int Id { get; init; }

    /// <summary>Build start time (X axis).</summary>
    public DateTimeOffset X { get; init; }

    /// <summary>Build duration in minutes (Y axis). May be 0 when duration is unknown.</summary>
    public double Y { get; init; }

    /// <summary>
    /// Result category.  Accepted values (case-insensitive):
    /// Succeeded | Failed | PartiallySucceeded | Canceled
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Build number / run name (e.g., "20260101.5").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Optional Build Quality state label used for point borders and drawer context.
    /// </summary>
    public string? QualityStateLabel { get; init; }

    /// <summary>
    /// Optional SVG/CSS color for the Build Quality border.
    /// </summary>
    public string? QualityStrokeColor { get; init; }

    /// <summary>
    /// Extended key/value metadata shown in the tooltip and Build Summary drawer.
    /// Recommended keys: PipelineName, FinishTime, Branch, Url, PipelineDefinitionId.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // ── Typed convenience accessors ───────────────────────────────────────

    /// <summary>Pipeline name from Metadata["PipelineName"].</summary>
    public string PipelineName =>
        Metadata.TryGetValue("PipelineName", out var v) ? v : string.Empty;

    /// <summary>
    /// Pipeline definition ID (int) from Metadata["PipelineDefinitionId"].
    /// Returns 0 when absent or unparseable. A value of 0 means "not available"
    /// and will never match a real pipeline ID in highlight filtering.
    /// </summary>
    public int PipelineDefinitionId =>
        Metadata.TryGetValue("PipelineDefinitionId", out var v) && int.TryParse(v, out var id) ? id : 0;

    /// <summary>Azure DevOps URL from Metadata["Url"]. Null when absent.</summary>
    public string? Url =>
        Metadata.TryGetValue("Url", out var v) ? v : null;

    /// <summary>Source branch from Metadata["Branch"]. Null when absent.</summary>
    public string? Branch =>
        Metadata.TryGetValue("Branch", out var v) ? v : null;

    /// <summary>Finish time (ISO string) from Metadata["FinishTime"]. Null when absent.</summary>
    public string? FinishTimeString =>
        Metadata.TryGetValue("FinishTime", out var v) ? v : null;
}
