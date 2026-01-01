using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Implementation of tree building logic for work item hierarchies.
/// </summary>
public class TreeBuilderService : ITreeBuilderService
{
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
                        Effort = dto.Effort
                    })
                };
                nodeMap[id] = node;
            }
            else
            {
                node.Title = dto.Title;
                node.Type = dto.Type;
                node.State = dto.State;
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
                    Effort = dto.Effort
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

        // Compute InvalidDescendantIds for all nodes
        foreach (var root in roots)
        {
            ComputeInvalidDescendantIds(root);
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
    /// Recursively computes InvalidDescendantIds for each node.
    /// Returns a list of invalid descendant IDs found in the subtree rooted at the given node.
    /// </summary>
    private List<int> ComputeInvalidDescendantIds(TreeNode node)
    {
        var invalidDescendants = new List<int>();

        // Process children in order (stable pre-order traversal)
        foreach (var child in node.Children)
        {
            // Check if child itself has issues
            if (child.SelfErrorCount > 0 || child.SelfWarningCount > 0)
            {
                invalidDescendants.Add(child.Id);
            }

            // Recursively collect invalid descendants from child's subtree
            var childInvalidDescendants = ComputeInvalidDescendantIds(child);
            invalidDescendants.AddRange(childInvalidDescendants);
        }

        // Sort by depth (closer descendants first), then maintain pre-order
        // Group by depth level
        var nodeMap = new Dictionary<int, TreeNode>();
        CollectNodesIntoMap(node, nodeMap);

        var sortedInvalidDescendants = invalidDescendants
            .Distinct()
            .Select(id => new { Id = id, Depth = nodeMap.ContainsKey(id) ? nodeMap[id].Level : int.MaxValue })
            .OrderBy(x => x.Depth)
            .ThenBy(x => invalidDescendants.IndexOf(x.Id)) // Maintain pre-order within same depth
            .Select(x => x.Id)
            .ToList();

        node.InvalidDescendantIds = sortedInvalidDescendants;

        return invalidDescendants;
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
}
