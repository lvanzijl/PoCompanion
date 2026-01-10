using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for FixValidationViolationBatchCommand.
/// Applies automated fixes to validation violations by updating work item states in TFS.
/// Uses bulk update method to prevent N+1 query pattern.
/// </summary>
public sealed class FixValidationViolationBatchCommandHandler
    : ICommandHandler<FixValidationViolationBatchCommand, FixValidationViolationResultDto>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<FixValidationViolationBatchCommandHandler> _logger;

    public FixValidationViolationBatchCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<FixValidationViolationBatchCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<FixValidationViolationResultDto> Handle(
        FixValidationViolationBatchCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling FixValidationViolationBatchCommand with {Count} fixes", command.Fixes.Count);

        var validUpdates = new List<WorkItemStateUpdate>();
        var validationResults = new List<FixResult>();
        var validationFailureCount = 0;

        // Step 1: Validate all fixes first
        foreach (var fix in command.Fixes)
        {
            _logger.LogDebug("Validating fix for work item {WorkItemId}: {FixType} - Set state to {NewState}",
                fix.WorkItemId, fix.FixType, fix.NewState);

            // Validate the work item exists locally
            var workItem = await _repository.GetByTfsIdAsync(fix.WorkItemId, cancellationToken);
            if (workItem == null)
            {
                var errorMsg = $"Work item {fix.WorkItemId} not found in local cache";
                _logger.LogWarning(errorMsg);
                validationResults.Add(new FixResult(fix.WorkItemId, false, errorMsg));
                validationFailureCount++;
                continue;
            }

            // Add to valid updates list for bulk processing
            validUpdates.Add(new WorkItemStateUpdate(fix.WorkItemId, fix.NewState));
        }

        // Step 2: Use bulk method to update all valid work items in a single call.
        // This prevents the N+1 pattern where we would call UpdateWorkItemStateAsync
        // for each work item individually.
        var bulkResult = await _tfsClient.UpdateWorkItemsStateAsync(validUpdates, cancellationToken);

        // Log performance instrumentation showing call reduction
        _logger.LogInformation(
            "Bulk state update completed with {TfsCallCount} TFS call(s) for {Count} work items",
            bulkResult.TfsCallCount,
            validUpdates.Count);

        // Combine validation failures with TFS update results
        var allResults = new List<FixResult>(validationResults);
        foreach (var updateResult in bulkResult.Results)
        {
            var successMsg = updateResult.Success
                ? $"Successfully updated work item {updateResult.WorkItemId}"
                : updateResult.ErrorMessage ?? $"Failed to update work item {updateResult.WorkItemId}";
            allResults.Add(new FixResult(updateResult.WorkItemId, updateResult.Success, successMsg));
        }

        var totalSuccess = bulkResult.SuccessfulUpdates;
        var totalFailure = validationFailureCount + bulkResult.FailedUpdates;

        var result = new FixValidationViolationResultDto(
            TotalAttempted: command.Fixes.Count,
            SuccessfulFixes: totalSuccess,
            FailedFixes: totalFailure,
            Results: allResults
        );

        _logger.LogInformation("Batch fix complete: {Success} successful, {Failed} failed out of {Total}",
            totalSuccess, totalFailure, command.Fixes.Count);

        return result;
    }
}
