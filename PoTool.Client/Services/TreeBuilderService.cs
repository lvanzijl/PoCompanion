using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using SharedValidation = PoTool.Shared.WorkItems;

namespace PoTool.Client.Services;

/// <summary>
/// Implementation of tree building logic for work item hierarchies.
/// </summary>
public class TreeBuilderService : ITreeBuilderService
{
    // Special node IDs for non-work-item nodes
    private const int ProductNodeIdOffset = -1000;  // Product nodes get IDs like -1001, -1002, etc.
    private const int UnparentedNodeId = -1;         // Unparented node gets ID -1

    /// <inheritdoc/>
    public List<TreeNode> BuildTree(IEnumerable<WorkItemDto> items, Dictionary<int, bool> expandedState)
    {
        var roots = new List<TreeNode>();
        var nodeMap = new Dictionary<int, TreeNode>();

        // Create node instances keyed by TfsId
        foreach (var dto in items)
        {
            var id = dto.TfsId;
            if (!nodeMap.TryGetValue(id, out var node))
            {
                node = new TreeNode
                {
                    Id = id,
                    Title = dto.Title,
                    Type = dto.Type,
                    State = dto.State,
                    IsDone = false, // State classification should be determined by StateClassificationService when needed
                    ParentId = dto.ParentTfsId,
                    JsonPayload = System.Text.Json.JsonSerializer.Serialize(dto)
                };
                nodeMap[id] = node;
            }
            else
            {
                node.Title = dto.Title;
                node.Type = dto.Type;
                node.State = dto.State;
                node.IsDone = false; // State classification should be determined by StateClassificationService when needed
                node.JsonPayload = System.Text.Json.JsonSerializer.Serialize(dto);
            }

            // Restore expanded state if available
            if (expandedState.TryGetValue(id, out var isExpanded))
            {
                node.IsExpanded = isExpanded;
            }
        }

        // Attach children to parents based on ParentTfsId, create placeholders for missing parents
        foreach (var dto in items)
        {
            var node = nodeMap[dto.TfsId];
            var parentId = dto.ParentTfsId;

            if (parentId.HasValue)
            {
                if (!nodeMap.TryGetValue(parentId.Value, out var parentNode))
                {
                    // Create placeholder parent node for missing parent
                    parentNode = new TreeNode
                    {
                        Id = parentId.Value,
                        Title = $"(missing) Parent #{parentId.Value}",
                        Type = "(missing)",
                        State = "",
                        ParentId = null
                    };
                    nodeMap[parentId.Value] = parentNode;
                }

                parentNode.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        // Sort children and roots
        foreach (var node in nodeMap.Values)
        {
            node.Children = node.Children.OrderBy(c => c.Title).ToList();
        }

        roots = roots.OrderBy(r => r.Title).ToList();

        return roots;
    }

    /// <inheritdoc/>
    public List<WorkItemDto> FilterWithAncestors(List<WorkItemDto> items, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return items;
        }

        // Create lookup dictionary for O(1) parent lookups
        var itemLookup = items.ToDictionary(w => w.TfsId);

        // Find matching items
        var matches = items
            .Where(w => w.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase));

        // Include ancestors for each match
        var toInclude = new Dictionary<int, WorkItemDto>();
        foreach (var match in matches)
        {
            if (!toInclude.ContainsKey(match.TfsId))
            {
                toInclude[match.TfsId] = match;
            }

            var current = match;
            while (current.ParentTfsId.HasValue)
            {
                var parentId = current.ParentTfsId.Value;
                if (itemLookup.TryGetValue(parentId, out var parent))
                {
                    if (!toInclude.ContainsKey(parent.TfsId))
                    {
                        toInclude[parent.TfsId] = parent;
                    }
                    current = parent;
                }
                else
                {
                    // Parent missing -> create placeholder later in tree builder; stop walking
                    break;
                }
            }
        }

        return toInclude.Values.ToList();
    }

    /// <inheritdoc/>
    public List<TreeNode> BuildTreeWithValidation(IEnumerable<WorkItemWithValidationDto> items, Dictionary<int, bool> expandedState)
    {
        var roots = new List<TreeNode>();
        var nodeMap = new Dictionary<int, TreeNode>();

        // Create node instances keyed by TfsId
        foreach (var dto in items)
        {
            var id = dto.TfsId;
            if (!nodeMap.TryGetValue(id, out var node))
            {
                node = new TreeNode
                {
                    Id = id,
                    Title = dto.Title,
                    Type = dto.Type,
                    State = dto.State,
                    IsDone = false, // State classification should be determined by StateClassificationService when needed
                    ParentId = dto.ParentTfsId,
                    JsonPayload = System.Text.Json.JsonSerializer.Serialize(new WorkItemDto
                    {
                        TfsId = dto.TfsId,
                        Type = dto.Type,
                        Title = dto.Title,
                        ParentTfsId = dto.ParentTfsId,
                        AreaPath = dto.AreaPath,
                        IterationPath = dto.IterationPath,
                        State = dto.State,
                        JsonPayload = dto.JsonPayload,
                        RetrievedAt = dto.RetrievedAt,
                        Effort = dto.Effort,
                        Description = dto.Description
                    })
                };
                nodeMap[id] = node;
            }
            else
            {
                node.Title = dto.Title;
                node.Type = dto.Type;
                node.State = dto.State;
                node.IsDone = false; // State classification should be determined by StateClassificationService when needed
                node.JsonPayload = System.Text.Json.JsonSerializer.Serialize(new WorkItemDto
                {
                    TfsId = dto.TfsId,
                    Type = dto.Type,
                    Title = dto.Title,
                    ParentTfsId = dto.ParentTfsId,
                    AreaPath = dto.AreaPath,
                    IterationPath = dto.IterationPath,
                    State = dto.State,
                    JsonPayload = dto.JsonPayload,
                    RetrievedAt = dto.RetrievedAt,
                    Effort = dto.Effort,
                    Description = dto.Description
                });
            }

            // Restore expanded state if available
            if (expandedState.TryGetValue(id, out var isExpanded))
            {
                node.IsExpanded = isExpanded;
            }

            // Populate validation issues from API
            node.ValidationIssues = dto.ValidationIssues
                .Select(vi => $"{vi.Severity}: {vi.Message}")
                .ToList();

            // Set highest severity (Error > Warning)
            if (dto.ValidationIssues.Any(vi => vi.Severity == "Error"))
            {
                node.HighestSeverity = "Error";
            }
            else if (dto.ValidationIssues.Any(vi => vi.Severity == "Warning"))
            {
                node.HighestSeverity = "Warning";
            }
            
            // Determine highest validation category
            node.HighestCategory = DetermineHighestCategory(dto.ValidationIssues);

            // Populate self error and warning counts
            node.SelfErrorCount = dto.ValidationIssues.Count(vi => vi.Severity == "Error");
            node.SelfWarningCount = dto.ValidationIssues.Count(vi => vi.Severity == "Warning");
        }

        // Attach children to parents based on ParentTfsId, create placeholders for missing parents
        foreach (var dto in items)
        {
            var node = nodeMap[dto.TfsId];
            var parentId = dto.ParentTfsId;

            if (parentId.HasValue)
            {
                if (!nodeMap.TryGetValue(parentId.Value, out var parentNode))
                {
                    // Create placeholder parent node for missing parent
                    parentNode = new TreeNode
                    {
                        Id = parentId.Value,
                        Title = $"(missing) Parent #{parentId.Value}",
                        Type = "(missing)",
                        State = "",
                        ParentId = null
                    };
                    nodeMap[parentId.Value] = parentNode;
                }

                parentNode.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        // Sort children and roots
        foreach (var node in nodeMap.Values)
        {
            node.Children = node.Children.OrderBy(c => c.Title).ToList();
        }

        roots = roots.OrderBy(r => r.Title).ToList();

        // Populate ChildrenIds for all nodes
        foreach (var node in nodeMap.Values)
        {
            node.ChildrenIds = node.Children.Select(c => c.Id).ToList();
        }

        // Compute depth/level for all nodes
        ComputeDepth(roots, 0);

        // Build global node map once for efficient lookups
        var globalNodeMap = new Dictionary<int, TreeNode>();
        foreach (var root in roots)
        {
            CollectNodesIntoMap(root, globalNodeMap);
        }

        // Compute InvalidDescendantIds for all nodes
        foreach (var root in roots)
        {
            ComputeInvalidDescendantIds(root, globalNodeMap);
        }

        return roots;
    }

    /// <summary>
    /// Recursively computes the depth/level for each node in the tree.
    /// </summary>
    private void ComputeDepth(List<TreeNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            node.Level = depth;
            if (node.Children.Any())
            {
                ComputeDepth(node.Children, depth + 1);
            }
        }
    }

    /// <summary>
    /// Recursively computes InvalidDescendantIds and HighestDescendantCategory for each node.
    /// Returns a list of invalid descendant IDs with their original insertion order.
    /// </summary>
    private List<(int Id, int Order)> ComputeInvalidDescendantIds(TreeNode node, Dictionary<int, TreeNode> nodeMap)
    {
        var invalidDescendants = new List<(int Id, int Order)>();
        int order = 0;
        SharedValidation.ValidationCategory? highestDescendantCategory = null;

        // Process children in order (stable pre-order traversal)
        foreach (var child in node.Children)
        {
            // Check if child itself has issues
            if (child.SelfErrorCount > 0 || child.SelfWarningCount > 0)
            {
                invalidDescendants.Add((child.Id, order++));
                
                // Update highest descendant category from direct child
                if (child.HighestCategory.HasValue)
                {
                    if (!highestDescendantCategory.HasValue || 
                        (int)child.HighestCategory.Value > (int)highestDescendantCategory.Value)
                    {
                        highestDescendantCategory = child.HighestCategory;
                    }
                }
            }

            // Recursively collect invalid descendants from child's subtree
            var childInvalidDescendants = ComputeInvalidDescendantIds(child, nodeMap);
            foreach (var (id, _) in childInvalidDescendants)
            {
                invalidDescendants.Add((id, order++));
            }
            
            // Also consider child's highest descendant category
            if (child.HighestDescendantCategory.HasValue)
            {
                if (!highestDescendantCategory.HasValue || 
                    (int)child.HighestDescendantCategory.Value > (int)highestDescendantCategory.Value)
                {
                    highestDescendantCategory = child.HighestDescendantCategory;
                }
            }
        }

        // Sort by depth (closer descendants first), then maintain pre-order using tracked order
        // Note: All IDs should exist in nodeMap since they're collected during tree traversal.
        // Using int.MaxValue as a fallback for defensive programming, though this should never occur.
        var sortedInvalidDescendants = invalidDescendants
            .Distinct()
            .Select(item => new
            {
                Id = item.Id,
                Depth = nodeMap.TryGetValue(item.Id, out var foundNode) ? foundNode.Level : int.MaxValue,
                Order = item.Order
            })
            .OrderBy(x => x.Depth)
            .ThenBy(x => x.Order)
            .Select(x => x.Id)
            .ToList();

        node.InvalidDescendantIds = sortedInvalidDescendants;
        node.HighestDescendantCategory = highestDescendantCategory;

        return invalidDescendants;
    }

    /// <summary>
    /// Recursively filters out tasks that don't have validation issues from all nodes' children.
    /// </summary>
    private static void FilterTasksWithoutIssues(List<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            // Remove tasks without validation issues from children
            node.Children = node.Children
                .Where(child => child.Type != "Task" || child.HasValidationIssues)
                .ToList();
            
            // Update ChildrenIds to match filtered children
            node.ChildrenIds = node.Children.Select(c => c.Id).ToList();
            
            // Recursively filter children's children
            if (node.Children.Any())
            {
                FilterTasksWithoutIssues(node.Children);
            }
        }
    }

    /// <summary>
    /// Collects all nodes in a subtree into a dictionary keyed by ID.
    /// </summary>
    private void CollectNodesIntoMap(TreeNode node, Dictionary<int, TreeNode> map)
    {
        map[node.Id] = node;
        foreach (var child in node.Children)
        {
            CollectNodesIntoMap(child, map);
        }
    }

    /// <inheritdoc/>
    public List<TreeNode> BuildProductBasedTreeWithValidation(
        IEnumerable<WorkItemWithValidationDto> items,
        IEnumerable<ProductDto> products,
        Dictionary<int, bool> expandedState)
    {
        var topLevelNodes = new List<TreeNode>();
        var itemsList = items.ToList();
        var productsList = products.ToList();
        
        // Create node instances keyed by TfsId
        var nodeMap = new Dictionary<int, TreeNode>();
        foreach (var dto in itemsList)
        {
            var id = dto.TfsId;
            if (!nodeMap.TryGetValue(id, out var node))
            {
                node = new TreeNode
                {
                    Id = id,
                    Title = dto.Title,
                    Type = dto.Type,
                    State = dto.State,
                    IsDone = false, // State classification should be determined by StateClassificationService when needed
                    ParentId = dto.ParentTfsId,
                    JsonPayload = System.Text.Json.JsonSerializer.Serialize(new WorkItemDto
                    {
                        TfsId = dto.TfsId,
                        Type = dto.Type,
                        Title = dto.Title,
                        ParentTfsId = dto.ParentTfsId,
                        AreaPath = dto.AreaPath,
                        IterationPath = dto.IterationPath,
                        State = dto.State,
                        JsonPayload = dto.JsonPayload,
                        RetrievedAt = dto.RetrievedAt,
                        Effort = dto.Effort,
                        Description = dto.Description
                    })
                };
                nodeMap[id] = node;
            }
            else
            {
                node.Title = dto.Title;
                node.Type = dto.Type;
                node.State = dto.State;
                node.IsDone = false; // State classification should be determined by StateClassificationService when needed
                node.JsonPayload = System.Text.Json.JsonSerializer.Serialize(new WorkItemDto
                {
                    TfsId = dto.TfsId,
                    Type = dto.Type,
                    Title = dto.Title,
                    ParentTfsId = dto.ParentTfsId,
                    AreaPath = dto.AreaPath,
                    IterationPath = dto.IterationPath,
                    State = dto.State,
                    JsonPayload = dto.JsonPayload,
                    RetrievedAt = dto.RetrievedAt,
                    Effort = dto.Effort,
                    Description = dto.Description
                });
            }

            // Restore expanded state if available
            if (expandedState.TryGetValue(id, out var isExpanded))
            {
                node.IsExpanded = isExpanded;
            }

            // Populate validation issues from API
            node.ValidationIssues = dto.ValidationIssues
                .Select(vi => $"{vi.Severity}: {vi.Message}")
                .ToList();

            // Set highest severity (Error > Warning)
            if (dto.ValidationIssues.Any(vi => vi.Severity == "Error"))
            {
                node.HighestSeverity = "Error";
            }
            else if (dto.ValidationIssues.Any(vi => vi.Severity == "Warning"))
            {
                node.HighestSeverity = "Warning";
            }
            
            // Determine highest validation category
            node.HighestCategory = DetermineHighestCategory(dto.ValidationIssues);

            // Populate self error and warning counts
            node.SelfErrorCount = dto.ValidationIssues.Count(vi => vi.Severity == "Error");
            node.SelfWarningCount = dto.ValidationIssues.Count(vi => vi.Severity == "Warning");
        }

        // Build parent-child relationships
        // Track which nodes have been attached to a parent to prevent duplication
        var attachedNodeIds = new HashSet<int>();
        var orphanedWorkItems = new List<TreeNode>(); // Items with a parent that doesn't exist in the dataset
        
        foreach (var dto in itemsList)
        {
            var node = nodeMap[dto.TfsId];
            var parentId = dto.ParentTfsId;

            if (parentId.HasValue)
            {
                if (nodeMap.TryGetValue(parentId.Value, out var parentNode))
                {
                    // Parent exists - attach to parent
                    if (!attachedNodeIds.Contains(node.Id))
                    {
                        parentNode.Children.Add(node);
                        attachedNodeIds.Add(node.Id);
                    }
                }
                else
                {
                    // Parent is missing - this is an orphan
                    if (!attachedNodeIds.Contains(node.Id))
                    {
                        orphanedWorkItems.Add(node);
                        attachedNodeIds.Add(node.Id);
                    }
                }
            }
            // else: No parent (parentless) - will become a root node
        }

        // Sort children for all nodes
        foreach (var node in nodeMap.Values)
        {
            node.Children = node.Children.OrderBy(c => c.Title).ToList();
        }
        
        // Filter tasks: only show tasks that have validation issues
        FilterTasksWithoutIssues(nodeMap.Values.ToList());

        // Collect root nodes: items without a parent AND not already attached
        // Parentless items that haven't been attached become roots
        var rootNodes = nodeMap.Values
            .Where(n => !n.ParentId.HasValue)
            .OrderBy(n => n.Title)
            .ToList();

        // Add root nodes directly to top level (no synthetic wrapper)
        topLevelNodes.AddRange(rootNodes);
        
        // Create Unparented node only if there are orphaned items (items with missing parent)
        if (orphanedWorkItems.Any())
        {
            var unparentedNode = new TreeNode
            {
                Id = UnparentedNodeId, // Special ID for Unparented node
                Title = "Unparented",
                Type = "Unparented",
                State = "",
                ParentId = null,
                Children = orphanedWorkItems.OrderBy(n => n.Title).ToList(),
                IsExpanded = expandedState.TryGetValue(UnparentedNodeId, out var isExpanded) && isExpanded
            };

            topLevelNodes.Add(unparentedNode);
        }

        // Populate ChildrenIds for all nodes
        foreach (var node in nodeMap.Values)
        {
            node.ChildrenIds = node.Children.Select(c => c.Id).ToList();
        }
        
        foreach (var topNode in topLevelNodes)
        {
            topNode.ChildrenIds = topNode.Children.Select(c => c.Id).ToList();
        }

        // Compute depth/level for all nodes
        ComputeDepth(topLevelNodes, 0);

        // Build global node map once for efficient lookups
        var globalNodeMap = new Dictionary<int, TreeNode>();
        foreach (var root in topLevelNodes)
        {
            CollectNodesIntoMap(root, globalNodeMap);
        }

        // Compute InvalidDescendantIds for all nodes
        foreach (var root in topLevelNodes)
        {
            ComputeInvalidDescendantIds(root, globalNodeMap);
        }

        return topLevelNodes;
    }
    
    /// <inheritdoc/>
    public async Task<List<TreeNode>> ApplyVisibilityFilterAsync(
        List<TreeNode> roots,
        WorkItemVisibilityService visibilityService,
        CancellationToken cancellationToken = default)
    {
        // Build a map of all nodes for efficient lookups
        var allNodes = new Dictionary<int, TreeNode>();
        foreach (var root in roots)
        {
            CollectNodesIntoMap(root, allNodes);
        }

        // Recursively filter the tree
        return await visibilityService.FilterHiddenNodesAsync(roots, allNodes, cancellationToken);
    }
    
    /// <summary>
    /// Determines the highest validation category from a list of validation issues.
    /// </summary>
    private static SharedValidation.ValidationCategory? DetermineHighestCategory(IEnumerable<ValidationIssue> issues)
    {
        if (!issues.Any())
        {
            return null;
        }
        
        SharedValidation.ValidationCategory? highest = null;
        
        foreach (var issue in issues)
        {
            SharedValidation.ValidationCategory? category = null;
            
            // Use RuleId directly to determine category
            if (!string.IsNullOrEmpty(issue.RuleId))
            {
                if (issue.RuleId.StartsWith("SI-"))
                {
                    category = SharedValidation.ValidationCategory.StructuralIntegrity;
                }
                else if (issue.RuleId.StartsWith("RR-"))
                {
                    category = SharedValidation.ValidationCategory.RefinementReadiness;
                }
                else if (issue.RuleId.StartsWith("RC-"))
                {
                    category = SharedValidation.ValidationCategory.RefinementCompleteness;
                }
            }
            
            // Only process issues with valid RuleId - skip issues without proper category
            if (!category.HasValue)
            {
                continue;
            }
            
            // Update highest if this category is higher priority
            // Priority: RefinementCompleteness (3) > RefinementReadiness (2) > StructuralIntegrity (1)
            if (!highest.HasValue || (int)category.Value > (int)highest.Value)
            {
                highest = category;
            }
        }
        
        return highest;
    }
}
