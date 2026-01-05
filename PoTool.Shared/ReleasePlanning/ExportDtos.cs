namespace PoTool.Shared.ReleasePlanning;

/// <summary>
/// Export format options.
/// </summary>
public enum ExportFormat
{
    Png = 0,
    Pdf = 1
}

/// <summary>
/// Paper size options for export.
/// </summary>
public enum PaperSize
{
    A4 = 0,
    A3 = 1
}

/// <summary>
/// Export layout options.
/// </summary>
public enum ExportLayout
{
    FitToPage = 0,
    MultiPage = 1
}

/// <summary>
/// Request for exporting the Release Planning Board.
/// </summary>
public sealed record ExportOptionsDto
{
    /// <summary>
    /// Export format (PNG or PDF).
    /// </summary>
    public ExportFormat Format { get; init; } = ExportFormat.Png;

    /// <summary>
    /// Paper size for export.
    /// </summary>
    public PaperSize PaperSize { get; init; } = PaperSize.A4;

    /// <summary>
    /// Layout mode.
    /// </summary>
    public ExportLayout Layout { get; init; } = ExportLayout.FitToPage;

    /// <summary>
    /// Title to display on export.
    /// </summary>
    public string Title { get; init; } = "Release Planning Board";

    /// <summary>
    /// Objective IDs to include in export. Empty means all.
    /// </summary>
    public IReadOnlyList<int> IncludedObjectiveIds { get; init; } = [];

    /// <summary>
    /// Include milestone lines in export.
    /// </summary>
    public bool IncludeMilestoneLines { get; init; } = true;

    /// <summary>
    /// Include iteration lines in export.
    /// </summary>
    public bool IncludeIterationLines { get; init; } = true;

    /// <summary>
    /// Optional start milestone ID for range filtering.
    /// </summary>
    public int? StartMilestoneId { get; init; }

    /// <summary>
    /// Optional end milestone ID for range filtering.
    /// </summary>
    public int? EndMilestoneId { get; init; }
}

/// <summary>
/// Result of an export operation.
/// </summary>
public sealed record ExportResultDto
{
    /// <summary>
    /// Whether the export was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the export failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The exported file content as base64 (for client-side download).
    /// </summary>
    public string? FileContentBase64 { get; init; }

    /// <summary>
    /// The suggested file name.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// MIME type of the exported file.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Number of pages in the export (for PDF).
    /// </summary>
    public int PageCount { get; init; } = 1;
}
