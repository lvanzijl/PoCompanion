using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetValidationImpactAnalysisQuery.
/// Analyzes the impact of validation violations on work item hierarchy and workflow.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetValidationImpactAnalysisQueryHandler
    : IQueryHandler<GetValidationImpactAnalysisQuery, ValidationImpactAnalysisDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly IWorkItemValidator _validator;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ILogger<GetValidationImpactAnalysisQueryHandler> _logger;

    public GetValidationImpactAnalysisQueryHandler(
        IWorkItemRepository repository,
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        IWorkItemValidator validator,
        IWorkItemStateClassificationService stateClassificationService,
        ILogger<GetValidationImpactAnalysisQueryHandler> logger)
    {
        _repository = repository;
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _validator = validator;
        _stateClassificationService = stateClassificationService;
        _logger = logger;
    }

    public async ValueTask<ValidationImpactAnalysisDto> Handle(
        GetValidationImpactAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetValidationImpactAnalysisQuery with AreaPathFilter={AreaPath}, IterationPathFilter={IterationPath}",
            query.AreaPathFilter, query.IterationPathFilter);

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> workItems;
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            var rootIds = productsList
                .Where(p => p.BacklogRootWorkItemId > 0)
                .Select(p => p.BacklogRootWorkItemId)
                .ToArray();

            if (rootIds.Length > 0)
            {
                _logger.LogDebug("Loading work items from {Count} product roots for impact analysis", rootIds.Length);
                workItems = await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken);
            }
            else
            {
                workItems = await _repository.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            workItems = await _repository.GetAllAsync(cancellationToken);
        }

        var workItemsList = workItems.ToList();

        // Apply filters
        if (!string.IsNullOrEmpty(query.AreaPathFilter))
        {
            workItemsList = workItemsList
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrEmpty(query.IterationPathFilter))
        {
            workItemsList = workItemsList
                .Where(wi => wi.IterationPath.Contains(query.IterationPathFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Run validation
        var validationResults = _validator.ValidateWorkItems(workItemsList);

        // Build lookup maps
        var workItemLookup = workItemsList.ToDictionary(wi => wi.TfsId);
        var childrenLookup = BuildChildrenLookup(workItemsList);

        // Analyze impact of each violation
        var violations = new List<ViolationImpact>();
        var affectedHierarchies = new HashSet<int>();

        foreach (var (workItemId, issues) in validationResults)
        {
            // Skip work items that were filtered out
            if (!workItemLookup.TryGetValue(workItemId, out var workItem))
                continue;

            // Find blocked children (direct children that cannot progress)
            var blockedChildren = GetBlockedChildren(workItemId, childrenLookup, workItemLookup);
            var blockedDescendants = GetAllDescendants(workItemId, childrenLookup);

            foreach (var issue in issues)
            {
                violations.Add(new ViolationImpact(
                    WorkItemId: workItemId,
                    WorkItemType: workItem.Type,
                    WorkItemTitle: workItem.Title,
                    ViolationType: "ParentProgress",
                    Severity: issue.Severity,
                    BlockedChildrenIds: blockedChildren,
                    BlockedDescendantIds: blockedDescendants
                ));
            }

            // Track root of affected hierarchy
            var root = GetHierarchyRoot(workItemId, workItemLookup);
            affectedHierarchies.Add(root);
        }

        // Generate recommendations
        var recommendations = GenerateRecommendations(violations, workItemLookup);

        var result = new ValidationImpactAnalysisDto(
            Violations: violations,
            TotalBlockedItems: violations.Sum(v => v.BlockedDescendantIds.Count),
            TotalAffectedHierarchies: affectedHierarchies.Count,
            Recommendations: recommendations
        );

        _logger.LogInformation("Impact analysis complete: {ViolationCount} violations, {BlockedItems} blocked items, {Recommendations} recommendations",
            violations.Count, result.TotalBlockedItems, recommendations.Count);

        return result;
    }

    private Dictionary<int, List<int>> BuildChildrenLookup(List<WorkItemDto> workItems)
    {
        var lookup = new Dictionary<int, List<int>>();

        foreach (var wi in workItems)
        {
            if (wi.ParentTfsId.HasValue)
            {
                if (!lookup.ContainsKey(wi.ParentTfsId.Value))
                {
                    lookup[wi.ParentTfsId.Value] = new List<int>();
                }
                lookup[wi.ParentTfsId.Value].Add(wi.TfsId);
            }
        }

        return lookup;
    }

    private IReadOnlyList<int> GetBlockedChildren(
        int parentId,
        Dictionary<int, List<int>> childrenLookup,
        Dictionary<int, WorkItemDto> workItemLookup)
    {
        if (!childrenLookup.ContainsKey(parentId))
            return Array.Empty<int>();

        var parent = workItemLookup[parentId];
        var blocked = new List<int>();

        foreach (var childId in childrenLookup[parentId])
        {
            var child = workItemLookup[childId];
            // A child is blocked if it's in progress but parent is not
            // Use state classification service to properly determine state
            var childIsInProgress = _stateClassificationService.IsInProgressStateAsync(child.Type, child.State).GetAwaiter().GetResult();
            var parentIsInProgress = _stateClassificationService.IsInProgressStateAsync(parent.Type, parent.State).GetAwaiter().GetResult();
            
            if (childIsInProgress && !parentIsInProgress)
            {
                blocked.Add(childId);
            }
        }

        return blocked;
    }

    private IReadOnlyList<int> GetAllDescendants(int parentId, Dictionary<int, List<int>> childrenLookup)
    {
        var descendants = new List<int>();
        var queue = new Queue<int>();

        if (childrenLookup.ContainsKey(parentId))
        {
            foreach (var childId in childrenLookup[parentId])
            {
                queue.Enqueue(childId);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            descendants.Add(current);

            if (childrenLookup.ContainsKey(current))
            {
                foreach (var childId in childrenLookup[current])
                {
                    queue.Enqueue(childId);
                }
            }
        }

        return descendants;
    }

    private int GetHierarchyRoot(int workItemId, Dictionary<int, WorkItemDto> workItemLookup)
    {
        var current = workItemId;
        var visited = new HashSet<int>();

        while (workItemLookup.TryGetValue(current, out var workItem) && workItem.ParentTfsId.HasValue)
        {
            // Prevent infinite loop in case of circular references
            if (visited.Contains(current))
                break;

            visited.Add(current);
            current = workItem.ParentTfsId.Value;
        }

        return current;
    }

    private IReadOnlyList<WorkflowRecommendation> GenerateRecommendations(
        List<ViolationImpact> violations,
        Dictionary<int, WorkItemDto> workItemLookup)
    {
        var recommendations = new List<WorkflowRecommendation>();

        // Group violations by parent items that need to be moved to "In Progress"
        var parentsNeedingProgress = violations
            .Where(v => v.Severity == "Error")
            .GroupBy(v => v.WorkItemId)
            .OrderByDescending(g => g.First().BlockedDescendantIds.Count)
            .ToList();

        if (parentsNeedingProgress.Any())
        {
            var topParents = parentsNeedingProgress.Take(5).Select(g => g.Key).ToList();
            recommendations.Add(new WorkflowRecommendation(
                RecommendationType: "SetParentsToInProgress",
                Description: $"Set {topParents.Count} parent work items to 'In Progress' to unblock {parentsNeedingProgress.Sum(g => g.First().BlockedDescendantIds.Count)} child items",
                AffectedWorkItemIds: topParents,
                Priority: 1
            ));
        }

        // Identify work items with warnings (ancestor issues)
        var ancestorIssues = violations
            .Where(v => v.Severity == "Warning")
            .ToList();

        if (ancestorIssues.Count > 0)
        {
            recommendations.Add(new WorkflowRecommendation(
                RecommendationType: "ReviewAncestorProgress",
                Description: $"Review {ancestorIssues.Count} work items with ancestor progress issues to ensure proper work hierarchy",
                AffectedWorkItemIds: ancestorIssues.Select(v => v.WorkItemId).Distinct().ToList(),
                Priority: 2
            ));
        }

        return recommendations;
    }
}
