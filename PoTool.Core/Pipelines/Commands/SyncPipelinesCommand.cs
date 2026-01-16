using Mediator;

namespace PoTool.Core.Pipelines.Commands;

/// <summary>
/// Command to synchronize pipelines from TFS to local cache.
/// </summary>
public sealed record SyncPipelinesCommand(
    int RunsPerPipeline = 50,
    List<int>? ProductIds = null,
    bool SyncDefinitions = true
) : ICommand<PipelineSyncResult>;
