using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="UpdateWorkItemIterationPathCommand"/>.
/// Updates IterationPath in TFS and refreshes the work item in the local cache.
/// </summary>
public sealed class UpdateWorkItemIterationPathCommandHandler
    : ICommandHandler<UpdateWorkItemIterationPathCommand, bool>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<UpdateWorkItemIterationPathCommandHandler> _logger;

    public UpdateWorkItemIterationPathCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<UpdateWorkItemIterationPathCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<bool> Handle(UpdateWorkItemIterationPathCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating work item {TfsId} IterationPath to {IterationPath}", command.TfsId, command.NewIterationPath);

        // Step 1: Write to TFS
        var success = await _tfsClient.UpdateWorkItemIterationPathAsync(command.TfsId, command.NewIterationPath, cancellationToken);
        if (!success)
        {
            _logger.LogWarning("Failed to update IterationPath for work item {TfsId} in TFS", command.TfsId);
            return false;
        }

        // Step 2: Refresh from TFS to get the authoritative state back into cache.
        // Always override IterationPath with the value we just wrote — TFS may return
        // stale data briefly after a write, and the mock client never mutates its state.
        var refreshed = await _tfsClient.GetWorkItemByIdAsync(command.TfsId, cancellationToken);
        if (refreshed != null)
        {
            var updated = refreshed with { IterationPath = command.NewIterationPath };
            await _repository.UpsertManyAsync(new[] { updated }, cancellationToken);
            _logger.LogInformation("Work item {TfsId} IterationPath updated to {IterationPath} and cache refreshed", command.TfsId, command.NewIterationPath);
        }
        else
        {
            // Fallback: update IterationPath from the existing cached entity so the
            // cache always reflects the value we just wrote to TFS.
            var existing = await _repository.GetByTfsIdAsync(command.TfsId, cancellationToken);
            if (existing != null)
            {
                var updated = existing with { IterationPath = command.NewIterationPath };
                await _repository.UpsertManyAsync(new[] { updated }, cancellationToken);
                _logger.LogInformation("Work item {TfsId} IterationPath updated to {IterationPath} in cache (TFS re-fetch unavailable)", command.TfsId, command.NewIterationPath);
            }
            else
            {
                _logger.LogWarning("Work item {TfsId} was updated in TFS but could not be refreshed in cache", command.TfsId);
            }
        }

        return true;
    }
}
