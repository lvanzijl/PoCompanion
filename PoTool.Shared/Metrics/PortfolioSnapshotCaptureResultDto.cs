namespace PoTool.Shared.Metrics;

public sealed record PortfolioSnapshotCaptureResultDto(
    int ProductCount,
    int SourceCount,
    int CreatedSnapshotCount,
    int ExistingSnapshotCount);
