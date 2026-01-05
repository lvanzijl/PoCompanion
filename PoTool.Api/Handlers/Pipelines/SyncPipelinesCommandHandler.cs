using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Core.Pipelines.Commands;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for SyncPipelinesCommand.
/// Synchronizes pipelines from TFS to local cache.
/// </summary>
public sealed class SyncPipelinesCommandHandler : ICommandHandler<SyncPipelinesCommand, PipelineSyncResult>
{
    private readonly IPipelineRepository _repository;
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<SyncPipelinesCommandHandler> _logger;

    public SyncPipelinesCommandHandler(
        IPipelineRepository repository,
        ITfsClient tfsClient,
        ILogger<SyncPipelinesCommandHandler> logger)
    {
        _repository = repository;
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<PipelineSyncResult> Handle(
        SyncPipelinesCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pipeline sync with {RunsPerPipeline} runs per pipeline", command.RunsPerPipeline);

        var syncResult = await _tfsClient.GetPipelinesWithRunsAsync(command.RunsPerPipeline, cancellationToken);

        _logger.LogInformation(
            "Pipeline fetch completed with {TfsCallCount} TFS call(s) - retrieved {PipelineCount} pipelines, {RunCount} runs",
            syncResult.TfsCallCount,
            syncResult.Pipelines.Count,
            syncResult.Runs.Count);

        // Save to repository
        await _repository.SaveAsync(syncResult, cancellationToken);

        _logger.LogInformation("Pipeline sync completed. Synced {Count} pipelines", syncResult.Pipelines.Count);
        return syncResult;
    }
}
