using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines;

/// <summary>
/// Result of syncing pipeline data from TFS.
/// </summary>
public record PipelineSyncResult(
    IReadOnlyList<PipelineDto> Pipelines,
    IReadOnlyList<PipelineRunDto> Runs,
    int TfsCallCount,
    DateTimeOffset SyncedAt
);
