using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Shared.BugTriage;
using Microsoft.Extensions.Logging;

namespace PoTool.Client.Services;

/// <summary>
/// Service for building bug-specific tree structures with triage groupings.
/// Extends TreeBuilderService patterns to create synthetic "New/Untriaged" and severity group nodes.
/// Groups are created dynamically based on actual severity values from TFS.
/// </summary>
public class BugTreeBuilderService
{
    private readonly ILogger<BugTreeBuilderService> _logger;
    
    // Special node IDs for synthetic grouping nodes
    private const int NewUntriagedNodeId = -1000;
    private const int MissingSeverityNodeId = -1;
    
    // Base ID for dynamic severity group nodes
    private const int SeverityGroupBaseId = -2000;

    public BugTreeBuilderService(ILogger<BugTreeBuilderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds a bug tree with synthetic grouping nodes for triage.
    /// Structure:
    /// - New / Untriaged (root group)
    ///   - Bug 1
    ///   - Bug 2
    /// - [Dynamic Severity Groups based on actual values]
    ///   - Bug 3
    /// - Missing/Invalid Severity (for bugs with issues)
    ///   - Bug 4
    /// </summary>
    public List<TreeNode> BuildBugTriageTree(
        IEnumerable<WorkItemWithValidationDto> bugs,
        HashSet<int> untriagedBugIds,
        Dictionary<int, bool> expandedState,
        Func<WorkItemWithValidationDto, string?> getSeverityFunc)
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

        // Group bugs by triage status
        var newUntriaged = bugs.Where(b => untriagedBugIds.Contains(b.TfsId)).ToList();
        var triaged = bugs.Where(b => !untriagedBugIds.Contains(b.TfsId)).ToList();

        // Sort bugs by ID descending (newest first) for consistent ordering
        newUntriaged = newUntriaged.OrderByDescending(b => b.TfsId).ToList();
        
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

        // Group triaged bugs by severity DYNAMICALLY
        var severityGroups = new Dictionary<string, List<WorkItemWithValidationDto>>();
        var bugsWithMissingSeverity = new List<WorkItemWithValidationDto>();
        
        foreach (var bug in triaged)
        {
            var severity = getSeverityFunc(bug);
            
            if (string.IsNullOrEmpty(severity))
            {
                // Track bugs with missing/invalid severity
                bugsWithMissingSeverity.Add(bug);
                _logger.LogWarning("Bug {BugId} has missing or invalid severity - will be grouped separately", bug.TfsId);
            }
            else
            {
                if (!severityGroups.ContainsKey(severity))
                {
                    severityGroups[severity] = new List<WorkItemWithValidationDto>();
                }
                severityGroups[severity].Add(bug);
            }
        }

        // Create severity group nodes dynamically based on actual values
        // Sort by severity value to maintain consistent ordering (1-Critical, 2-High, etc.)
        var orderedSeverities = severityGroups.Keys.OrderBy(s => s).ToList();
        
        int groupIdCounter = SeverityGroupBaseId;
        foreach (var severity in orderedSeverities)
        {
            var bugsInGroup = severityGroups[severity].OrderByDescending(b => b.TfsId).ToList();
            
            var severityNode = CreateGroupNode(
                groupIdCounter--,
                $"{severity} ({bugsInGroup.Count})",
                expandedState);
            
            foreach (var bug in bugsInGroup)
            {
                var bugNode = bugNodes[bug.TfsId];
                bugNode.ParentId = severityNode.Id;
                bugNode.Level = 1;
                severityNode.Children.Add(bugNode);
            }
            
            roots.Add(severityNode);
        }

        // Create "Missing/Invalid Severity" group if there are bugs with issues
        if (bugsWithMissingSeverity.Any())
        {
            var missingSeverityNode = CreateGroupNode(
                MissingSeverityNodeId,
                $"⚠️ Missing/Invalid Severity ({bugsWithMissingSeverity.Count})",
                expandedState);
            
            var sortedMissingBugs = bugsWithMissingSeverity.OrderByDescending(b => b.TfsId).ToList();
            foreach (var bug in sortedMissingBugs)
            {
                var bugNode = bugNodes[bug.TfsId];
                bugNode.ParentId = MissingSeverityNodeId;
                bugNode.Level = 1;
                missingSeverityNode.Children.Add(bugNode);
            }
            
            roots.Add(missingSeverityNode);
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
