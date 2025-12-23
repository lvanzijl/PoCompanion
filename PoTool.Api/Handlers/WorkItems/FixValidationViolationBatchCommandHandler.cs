using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for FixValidationViolationBatchCommand.
/// Applies automated fixes to validation violations by updating work item states in TFS.
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

        var results = new List<FixResult>();
        var successCount = 0;
        var failureCount = 0;

        foreach (var fix in command.Fixes)
        {
            try
            {
                _logger.LogDebug("Attempting fix for work item {WorkItemId}: {FixType} - Set state to {NewState}",
                    fix.WorkItemId, fix.FixType, fix.NewState);

                // Validate the work item exists locally
                var workItem = await _repository.GetByTfsIdAsync(fix.WorkItemId, cancellationToken);
                if (workItem == null)
                {
                    var errorMsg = $"Work item {fix.WorkItemId} not found in local cache";
                    _logger.LogWarning(errorMsg);
                    results.Add(new FixResult(fix.WorkItemId, false, errorMsg));
                    failureCount++;
                    continue;
                }

                // Apply fix to TFS
                var success = await _tfsClient.UpdateWorkItemStateAsync(
                    fix.WorkItemId,
                    fix.NewState,
                    cancellationToken);

                if (success)
                {
                    var successMsg = $"Successfully updated work item {fix.WorkItemId} to state '{fix.NewState}'";
                    _logger.LogInformation(successMsg);
                    results.Add(new FixResult(fix.WorkItemId, true, successMsg));
                    successCount++;
                }
                else
                {
                    var errorMsg = $"Failed to update work item {fix.WorkItemId} - TFS update returned false";
                    _logger.LogWarning(errorMsg);
                    results.Add(new FixResult(fix.WorkItemId, false, errorMsg));
                    failureCount++;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Exception updating work item {fix.WorkItemId}: {ex.Message}";
                _logger.LogError(ex, "Error applying fix for work item {WorkItemId}", fix.WorkItemId);
                results.Add(new FixResult(fix.WorkItemId, false, errorMsg));
                failureCount++;
            }
        }

        var result = new FixValidationViolationResultDto(
            TotalAttempted: command.Fixes.Count,
            SuccessfulFixes: successCount,
            FailedFixes: failureCount,
            Results: results
        );

        _logger.LogInformation("Batch fix complete: {Success} successful, {Failed} failed out of {Total}",
            successCount, failureCount, command.Fixes.Count);

        return result;
    }
}
