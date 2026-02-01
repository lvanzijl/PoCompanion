using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.BugTriage;

namespace PoTool.Client.Services;

/// <summary>
/// Service for building bug-specific tree structures with triage groupings.
/// Extends TreeBuilderService patterns to create synthetic "New/Untriaged" and criticality group nodes.
/// </summary>
public class BugTreeBuilderService
{
    // Special node IDs for synthetic grouping nodes
    private const int NewUntriagedNodeId = -1000;
    private const int CriticalGroupNodeId = -1001;
    private const int HighGroupNodeId = -1002;
    private const int MediumGroupNodeId = -1003;
    private const int LowGroupNodeId = -1004;

    /// <summary>
    /// Builds a bug tree with synthetic grouping nodes for triage.
    /// Structure:
    /// - New / Untriaged (root group)
    ///   - Bug 1
    ///   - Bug 2
    /// - Critical (root group)
    ///   - Bug 3
    /// - High (root group)
    ///   - Bug 4
    /// </summary>
    public List<TreeNode> BuildBugTriageTree(
        IEnumerable<WorkItemWithValidationDto> bugs,
        HashSet<int> untriagedBugIds,
        Dictionary<int, bool> expandedState,
        Func<WorkItemWithValidationDto, string> getCriticalityFunc)
    {
        var roots = new List<TreeNode>();
        var bugNodes = new Dictionary<int, TreeNode>();

        // Create bug nodes
        foreach (var bug in bugs)
        {
            var node = new TreeNode
            {
                Id = bug.TfsId,
                Title = bug.Title,
                Type = bug.Type,
                State = bug.State,
                ParentId = null, // Bugs are always top-level in triage view
                JsonPayload = System.Text.Json.JsonSerializer.Serialize(bug),
                IsExpanded = expandedState.GetValueOrDefault(bug.TfsId, false)
            };
            bugNodes[bug.TfsId] = node;
        }

        // Group bugs by triage status and criticality
        var newUntriaged = bugs.Where(b => untriagedBugIds.Contains(b.TfsId)).ToList();
        var triaged = bugs.Where(b => !untriagedBugIds.Contains(b.TfsId)).ToList();

        // Sort bugs by ID descending (newest first) for consistent ordering
        newUntriaged = newUntriaged.OrderByDescending(b => b.TfsId).ToList();
        
        // Group triaged bugs by criticality
        var critical = triaged.Where(b => getCriticalityFunc(b) == BugCriticality.Critical).OrderByDescending(b => b.TfsId).ToList();
        var high = triaged.Where(b => getCriticalityFunc(b) == BugCriticality.High).OrderByDescending(b => b.TfsId).ToList();
        var medium = triaged.Where(b => getCriticalityFunc(b) == BugCriticality.Medium).OrderByDescending(b => b.TfsId).ToList();
        var low = triaged.Where(b => getCriticalityFunc(b) == BugCriticality.Low).OrderByDescending(b => b.TfsId).ToList();

        // Create "New / Untriaged" root group if there are untriaged bugs
        if (newUntriaged.Any())
        {
            var newUntriagedNode = CreateGroupNode(
                NewUntriagedNodeId,
                $"New / Untriaged ({newUntriaged.Count})",
                expandedState);
            
            foreach (var bug in newUntriaged)
            {
                var bugNode = bugNodes[bug.TfsId];
                bugNode.ParentId = NewUntriagedNodeId;
                bugNode.Level = 1;
                newUntriagedNode.Children.Add(bugNode);
            }
            
            roots.Add(newUntriagedNode);
        }

        // Create criticality group nodes
        if (critical.Any())
        {
            var criticalNode = CreateGroupNode(
                CriticalGroupNodeId,
                $"Critical ({critical.Count})",
                expandedState);
            
            foreach (var bug in critical)
            {
                var bugNode = bugNodes[bug.TfsId];
                bugNode.ParentId = CriticalGroupNodeId;
                bugNode.Level = 1;
                criticalNode.Children.Add(bugNode);
            }
            
            roots.Add(criticalNode);
        }

        if (high.Any())
        {
            var highNode = CreateGroupNode(
                HighGroupNodeId,
                $"High ({high.Count})",
                expandedState);
            
            foreach (var bug in high)
            {
                var bugNode = bugNodes[bug.TfsId];
                bugNode.ParentId = HighGroupNodeId;
                bugNode.Level = 1;
                highNode.Children.Add(bugNode);
            }
            
            roots.Add(highNode);
        }

        if (medium.Any())
        {
            var mediumNode = CreateGroupNode(
                MediumGroupNodeId,
                $"Medium ({medium.Count})",
                expandedState);
            
            foreach (var bug in medium)
            {
                var bugNode = bugNodes[bug.TfsId];
                bugNode.ParentId = MediumGroupNodeId;
                bugNode.Level = 1;
                mediumNode.Children.Add(bugNode);
            }
            
            roots.Add(mediumNode);
        }

        if (low.Any())
        {
            var lowNode = CreateGroupNode(
                LowGroupNodeId,
                $"Low ({low.Count})",
                expandedState);
            
            foreach (var bug in low)
            {
                var bugNode = bugNodes[bug.TfsId];
                bugNode.ParentId = LowGroupNodeId;
                bugNode.Level = 1;
                lowNode.Children.Add(bugNode);
            }
            
            roots.Add(lowNode);
        }

        return roots;
    }

    /// <summary>
    /// Applies tag filters to bugs.
    /// </summary>
    public IEnumerable<WorkItemWithValidationDto> ApplyTagFilters(
        IEnumerable<WorkItemWithValidationDto> bugs,
        List<string> selectedTags,
        TagMatchMode matchMode,
        Func<WorkItemWithValidationDto, List<string>> getTagsFunc)
    {
        if (!selectedTags.Any())
        {
            return bugs;
        }

        return matchMode == TagMatchMode.Any
            ? bugs.Where(b => getTagsFunc(b).Any(t => selectedTags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            : bugs.Where(b => selectedTags.All(st => getTagsFunc(b).Any(t => t.Equals(st, StringComparison.OrdinalIgnoreCase))));
    }

    private static TreeNode CreateGroupNode(int id, string title, Dictionary<int, bool> expandedState)
    {
        return new TreeNode
        {
            Id = id,
            Title = title,
            Type = "(group)",
            State = string.Empty,
            ParentId = null,
            Level = 0,
            IsExpanded = expandedState.GetValueOrDefault(id, true) // Groups default to expanded
        };
    }
}
