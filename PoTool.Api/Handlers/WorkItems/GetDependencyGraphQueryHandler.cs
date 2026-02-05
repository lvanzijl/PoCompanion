using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetDependencyGraphQuery.
/// Builds a dependency graph from work item relationships.
/// Uses product-scoped hierarchical loading when products are configured.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetDependencyGraphQueryHandler
    : IQueryHandler<GetDependencyGraphQuery, DependencyGraphDto>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<GetDependencyGraphQueryHandler> _logger;

    public GetDependencyGraphQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        ILogger<GetDependencyGraphQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async ValueTask<DependencyGraphDto> Handle(
        GetDependencyGraphQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetDependencyGraphQuery");

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;
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
                _logger.LogDebug("Loading work items from {Count} product roots for dependency analysis", rootIds.Length);
                allWorkItems = await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken);
            }
            else
            {
                allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
        }

        var workItemsList = allWorkItems.ToList();

        // Filter work items if specified
        IEnumerable<WorkItemDto> filteredItems = workItemsList;

        if (!string.IsNullOrWhiteSpace(query.AreaPathFilter))
        {
            filteredItems = filteredItems.Where(wi =>
                wi.AreaPath != null &&
                wi.AreaPath.Contains(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (query.WorkItemIds != null && query.WorkItemIds.Any())
        {
            filteredItems = filteredItems.Where(wi => query.WorkItemIds.Contains(wi.TfsId));
        }

        if (query.WorkItemTypes != null && query.WorkItemTypes.Any())
        {
            filteredItems = filteredItems.Where(wi =>
                query.WorkItemTypes.Contains(wi.Type, StringComparer.OrdinalIgnoreCase));
        }

        var relevantWorkItems = filteredItems.ToList();

        // Build nodes and links
        var nodes = new List<DependencyNode>();
        var links = new List<DependencyLink>();
        var workItemMap = workItemsList.ToDictionary(wi => wi.TfsId);

        foreach (var workItem in relevantWorkItems)
        {
            // Use Relations property directly instead of parsing JsonPayload
            var relations = workItem.Relations ?? new List<WorkItemRelation>();

            var dependencyCount = relations.Count(r => r.LinkType == "System.LinkTypes.Dependency-Forward");
            var dependentCount = relations.Count(r => r.LinkType == "System.LinkTypes.Dependency-Reverse");
            var isBlocking = relations.Any(r => r.LinkType == "System.LinkTypes.Dependency-Reverse");

            nodes.Add(new DependencyNode(
                WorkItemId: workItem.TfsId,
                Title: workItem.Title,
                Type: workItem.Type,
                State: workItem.State,
                Effort: workItem.Effort,
                DependencyCount: dependencyCount,
                DependentCount: dependentCount,
                IsBlocking: isBlocking
            ));

            // Add links
            foreach (var relation in relations)
            {
                var linkType = MapLinkType(relation.LinkType);
                var targetId = relation.TargetWorkItemId;

                if (targetId.HasValue && workItemMap.ContainsKey(targetId.Value))
                {
                    links.Add(new DependencyLink(
                        SourceWorkItemId: workItem.TfsId,
                        TargetWorkItemId: targetId.Value,
                        LinkType: linkType,
                        Description: relation.LinkType
                    ));
                }
            }
        }

        // Find critical paths (longest dependency chains)
        var criticalPaths = FindCriticalPaths(nodes, links, workItemMap);

        // Detect circular dependencies
        var circularDependencies = DetectCircularDependencies(nodes, links);

        // Identify blocked work items
        var blockedWorkItemIds = nodes
            .Where(n => n.IsBlocking && n.DependentCount > 0)
            .Select(n => n.WorkItemId)
            .ToList();

        return new DependencyGraphDto(
            Nodes: nodes,
            Links: links,
            CriticalPaths: criticalPaths,
            BlockedWorkItemIds: blockedWorkItemIds,
            CircularDependencies: circularDependencies,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static DependencyLinkType MapLinkType(string linkType)
    {
        return linkType.ToLower() switch
        {
            string s when s.Contains("dependency-forward") => DependencyLinkType.DependsOn,
            string s when s.Contains("dependency-reverse") => DependencyLinkType.Blocks,
            string s when s.Contains("parent") => DependencyLinkType.Parent,
            string s when s.Contains("child") => DependencyLinkType.Child,
            _ => DependencyLinkType.RelatedTo
        };
    }

    private static List<DependencyChain> FindCriticalPaths(
        List<DependencyNode> nodes,
        List<DependencyLink> links,
        Dictionary<int, WorkItemDto> workItemMap)
    {
        var chains = new List<DependencyChain>();

        // Build adjacency list for dependency graph
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var link in links.Where(l => l.LinkType == DependencyLinkType.DependsOn))
        {
            if (!adjacency.ContainsKey(link.SourceWorkItemId))
                adjacency[link.SourceWorkItemId] = new List<int>();

            adjacency[link.SourceWorkItemId].Add(link.TargetWorkItemId);
        }

        // Find all paths using DFS - use fresh visited set for each starting node
        foreach (var node in nodes)
        {
            var visited = new HashSet<int>();
            var path = new List<int>();
            FindLongestPath(node.WorkItemId, adjacency, visited, path, chains, workItemMap);
        }

        // Return top 5 longest chains
        return chains
            .OrderByDescending(c => c.ChainLength)
            .ThenByDescending(c => c.TotalEffort)
            .Take(5)
            .ToList();
    }

    private static void FindLongestPath(
        int currentId,
        Dictionary<int, List<int>> adjacency,
        HashSet<int> visited,
        List<int> currentPath,
        List<DependencyChain> chains,
        Dictionary<int, WorkItemDto> workItemMap)
    {
        if (visited.Contains(currentId))
            return; // Avoid cycles

        visited.Add(currentId);
        currentPath.Add(currentId);

        if (adjacency.ContainsKey(currentId))
        {
            foreach (var neighbor in adjacency[currentId])
            {
                FindLongestPath(neighbor, adjacency, visited, currentPath, chains, workItemMap);
            }
        }
        else if (currentPath.Count >= 2) // Only add chains with at least 2 items
        {
            var totalEffort = currentPath
                .Where(id => workItemMap.ContainsKey(id) && workItemMap[id].Effort.HasValue)
                .Sum(id => workItemMap[id].Effort!.Value);

            var riskLevel = DetermineRiskLevel(currentPath.Count, totalEffort);

            chains.Add(new DependencyChain(
                WorkItemIds: currentPath.ToList(),
                TotalEffort: totalEffort,
                ChainLength: currentPath.Count,
                RiskLevel: riskLevel
            ));
        }

        currentPath.RemoveAt(currentPath.Count - 1);
        visited.Remove(currentId);
    }

    private static DependencyChainRisk DetermineRiskLevel(int chainLength, int totalEffort)
    {
        if (chainLength >= 5 || totalEffort >= 50)
            return DependencyChainRisk.Critical;
        if (chainLength >= 4 || totalEffort >= 30)
            return DependencyChainRisk.High;
        if (chainLength >= 3 || totalEffort >= 15)
            return DependencyChainRisk.Medium;
        return DependencyChainRisk.Low;
    }

    private static List<CircularDependency> DetectCircularDependencies(
        List<DependencyNode> nodes,
        List<DependencyLink> links)
    {
        var circularDependencies = new List<CircularDependency>();
        var seenCycles = new HashSet<string>(); // Use normalized string representation for O(1) lookup

        // Build adjacency list for dependency links only (not parent-child)
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var link in links.Where(l => l.LinkType == DependencyLinkType.DependsOn || l.LinkType == DependencyLinkType.Blocks))
        {
            if (!adjacency.ContainsKey(link.SourceWorkItemId))
                adjacency[link.SourceWorkItemId] = new List<int>();

            adjacency[link.SourceWorkItemId].Add(link.TargetWorkItemId);
        }

        var visited = new HashSet<int>();
        var recursionStack = new HashSet<int>();
        var currentPath = new List<int>();

        foreach (var node in nodes)
        {
            if (!visited.Contains(node.WorkItemId))
            {
                DetectCyclesFromNode(
                    node.WorkItemId,
                    adjacency,
                    visited,
                    recursionStack,
                    currentPath,
                    circularDependencies,
                    seenCycles);
            }
        }

        return circularDependencies;
    }

    private static void DetectCyclesFromNode(
        int nodeId,
        Dictionary<int, List<int>> adjacency,
        HashSet<int> visited,
        HashSet<int> recursionStack,
        List<int> currentPath,
        List<CircularDependency> circularDependencies,
        HashSet<string> seenCycles)
    {
        visited.Add(nodeId);
        recursionStack.Add(nodeId);
        currentPath.Add(nodeId);

        if (adjacency.ContainsKey(nodeId))
        {
            foreach (var neighbor in adjacency[nodeId])
            {
                if (!visited.Contains(neighbor))
                {
                    DetectCyclesFromNode(neighbor, adjacency, visited, recursionStack, currentPath, circularDependencies, seenCycles);
                }
                else if (recursionStack.Contains(neighbor))
                {
                    // Found a cycle - extract the cycle path
                    var cycleStartIndex = currentPath.IndexOf(neighbor);
                    var cycleIds = currentPath.Skip(cycleStartIndex).ToList();
                    cycleIds.Add(neighbor); // Close the cycle

                    // Create normalized cycle key (sorted IDs joined as string) for O(1) duplicate detection
                    var normalizedKey = string.Join(",", cycleIds.OrderBy(id => id));

                    if (!seenCycles.Contains(normalizedKey))
                    {
                        seenCycles.Add(normalizedKey);
                        var description = $"Circular dependency detected: {string.Join(" → ", cycleIds)}";

                        circularDependencies.Add(new CircularDependency(
                            CycleWorkItemIds: cycleIds,
                            Description: description));
                    }
                }
            }
        }

        currentPath.RemoveAt(currentPath.Count - 1);
        recursionStack.Remove(nodeId);
    }
}
