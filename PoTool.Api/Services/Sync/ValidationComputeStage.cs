using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that computes validation results for cached work items.
/// </summary>
public class ValidationComputeStage : ISyncStage
{
    private readonly PoToolDbContext _context;
    private readonly IHierarchicalWorkItemValidator _validator;
    private readonly ILogger<ValidationComputeStage> _logger;

    public string StageName => "ComputeValidations";
    public int StageNumber => 6;

    public ValidationComputeStage(
        PoToolDbContext context,
        IHierarchicalWorkItemValidator validator,
        ILogger<ValidationComputeStage> logger)
    {
        _context = context;
        _validator = validator;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);

            _logger.LogInformation(
                "Starting validation computation for ProductOwner {ProductOwnerId}",
                context.ProductOwnerId);

            // Get all cached work items for this ProductOwner's products
            var productIds = await _context.Products
                .Where(p => p.ProductOwnerId == context.ProductOwnerId)
                .Select(p => p.BacklogRootWorkItemId)
                .ToListAsync(cancellationToken);

            if (productIds.Count == 0)
            {
                _logger.LogInformation("No products found for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0);
            }

            // Load all work items that belong to these product trees
            // We need to traverse the tree to find all work items
            var allWorkItems = await _context.WorkItems
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Convert to DTOs
            var workItemDtos = allWorkItems.Select(MapToDto).ToList();

            progressCallback(20);

            // Run validation on each product tree
            var validationResults = new List<(int WorkItemId, int Indicator)>();
            var processedTrees = 0;
            var totalTrees = productIds.Count;

            foreach (var rootId in productIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var treeResult = _validator.ValidateTree(rootId, workItemDtos);
                    
                    // Determine indicator for the root work item
                    // 0 = No issues, 1 = Warning (refinement blockers), 2 = Error (structural integrity issues)
                    var indicator = 0;
                    if (treeResult.HasBacklogHealthProblems)
                    {
                        indicator = 2; // Error
                    }
                    else if (treeResult.HasRefinementBlockers || treeResult.HasIncompleteRefinement)
                    {
                        indicator = 1; // Warning
                    }

                    validationResults.Add((rootId, indicator));

                    // Also add indicators for work items with individual violations
                    foreach (var violation in treeResult.AllViolations)
                    {
                        if (violation.WorkItemId != rootId)
                        {
                            // Determine indicator based on category: StructuralIntegrity = Error (2), others = Warning (1)
                            var violationIndicator = violation.Rule.Category == ValidationCategory.StructuralIntegrity ? 2 : 1;
                            validationResults.Add((violation.WorkItemId, violationIndicator));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Validation failed for tree rooted at {RootId}", rootId);
                    // Continue with other trees
                }

                processedTrees++;
                var percent = 20 + (int)((processedTrees / (double)totalTrees) * 60);
                progressCallback(percent);
            }

            progressCallback(80);

            // Upsert validation results
            var upsertedCount = await UpsertValidationResultsAsync(validationResults, cancellationToken);

            progressCallback(100);

            _logger.LogInformation(
                "Successfully computed {Count} validation results for ProductOwner {ProductOwnerId}",
                upsertedCount,
                context.ProductOwnerId);

            return SyncStageResult.CreateSuccess(upsertedCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Validation compute cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation compute failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task<int> UpsertValidationResultsAsync(
        List<(int WorkItemId, int Indicator)> results,
        CancellationToken cancellationToken)
    {
        // Deduplicate - take worst indicator for each work item
        var grouped = results
            .GroupBy(r => r.WorkItemId)
            .Select(g => (WorkItemId: g.Key, Indicator: g.Max(r => r.Indicator)))
            .ToList();

        var workItemIds = grouped.Select(r => r.WorkItemId).ToList();
        
        // Load all existing entities in a single query
        var existingEntities = await _context.CachedValidationResults
            .Where(v => workItemIds.Contains(v.EpicId))
            .ToDictionaryAsync(v => v.EpicId, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        foreach (var (workItemId, indicator) in grouped)
        {
            if (existingEntities.TryGetValue(workItemId, out var entity))
            {
                entity.Indicator = indicator;
                entity.LastUpdated = now;
            }
            else
            {
                var newEntity = new CachedValidationResultEntity
                {
                    EpicId = workItemId,
                    Indicator = indicator,
                    LastUpdated = now
                };
                await _context.CachedValidationResults.AddAsync(newEntity, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return grouped.Count;
    }

    private static WorkItemDto MapToDto(WorkItemEntity entity)
    {
        List<WorkItemRelation>? relations = null;
        if (!string.IsNullOrEmpty(entity.Relations))
        {
            try
            {
                relations = System.Text.Json.JsonSerializer.Deserialize<List<WorkItemRelation>>(entity.Relations);
            }
            catch (System.Text.Json.JsonException)
            {
                // Ignore deserialization errors
            }
        }

        return new WorkItemDto(
            TfsId: entity.TfsId,
            Type: entity.Type,
            Title: entity.Title,
            ParentTfsId: entity.ParentTfsId,
            AreaPath: entity.AreaPath,
            IterationPath: entity.IterationPath,
            State: entity.State,
            RetrievedAt: entity.RetrievedAt,
            Effort: entity.Effort,
            Description: entity.Description,
            CreatedDate: entity.CreatedDate,
            ClosedDate: entity.ClosedDate,
            Severity: entity.Severity,
            Tags: entity.Tags,
            IsBlocked: entity.IsBlocked,
            Relations: relations
        );
    }
}
