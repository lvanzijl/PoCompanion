using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Core.Domain.BacklogQuality.Services;

/// <summary>
/// Executes canonical backlog-quality rules in the fixed domain order and aggregates their outputs.
/// </summary>
public sealed class BacklogValidationService
{
    private const string MissingEffortSemanticTag = "MissingEffort";
    private readonly RuleCatalog _ruleCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogValidationService"/> class.
    /// </summary>
    public BacklogValidationService()
        : this(new RuleCatalog())
    {
    }

    internal BacklogValidationService(RuleCatalog ruleCatalog)
    {
        _ruleCatalog = ruleCatalog ?? throw new ArgumentNullException(nameof(ruleCatalog));
    }

    /// <summary>
    /// Validates the backlog graph using the canonical backlog-quality rule families.
    /// </summary>
    public BacklogValidationResult Validate(BacklogGraph backlogGraph)
    {
        ArgumentNullException.ThrowIfNull(backlogGraph);

        var integrityFindings = ExecuteStructuralIntegrity(backlogGraph);
        var refinementFindings = ExecuteFamily(backlogGraph, RuleFamily.RefinementReadiness);
        var implementationFindings = ExecuteFamily(backlogGraph, RuleFamily.ImplementationReadiness);

        var refinementStates = BuildRefinementStates(backlogGraph, refinementFindings);
        var implementationStates = BuildImplementationStates(backlogGraph, implementationFindings);
        var suppressedImplementationIds = GetSuppressedImplementationIds(backlogGraph, refinementStates);
        var reportedImplementationFindings = implementationFindings
            .Where(finding => !suppressedImplementationIds.Contains(finding.WorkItemId))
            .ToArray();

        var findings = integrityFindings
            .Cast<ValidationRuleResult>()
            .Concat(refinementFindings)
            .Concat(reportedImplementationFindings)
            .ToArray();

        return new BacklogValidationResult(
            integrityFindings,
            findings,
            refinementStates,
            implementationStates);
    }

    private IReadOnlyList<BacklogIntegrityFinding> ExecuteStructuralIntegrity(BacklogGraph backlogGraph)
    {
        return ExecuteFamily(backlogGraph, RuleFamily.StructuralIntegrity)
            .Select(result => result as BacklogIntegrityFinding
                ?? throw new InvalidOperationException(
                    $"Structural integrity rule '{result.Rule.RuleId}' must return {nameof(BacklogIntegrityFinding)} results."))
            .ToArray();
    }

    private IReadOnlyList<ValidationRuleResult> ExecuteFamily(BacklogGraph backlogGraph, RuleFamily family)
    {
        return _ruleCatalog.GetByFamily(family)
            .SelectMany(rule => rule.Evaluate(backlogGraph))
            .ToArray();
    }

    private static IReadOnlyList<RefinementReadinessState> BuildRefinementStates(
        BacklogGraph backlogGraph,
        IReadOnlyList<ValidationRuleResult> refinementFindings)
    {
        var findingsByWorkItemId = refinementFindings
            .GroupBy(finding => finding.WorkItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ValidationRuleResult>)group.ToArray());

        return backlogGraph.Items
            .Where(IsRefinementScope)
            .Where(IsActive)
            .OrderBy(item => item.WorkItemId)
            .Select(item =>
            {
                var blockingFindings = findingsByWorkItemId.TryGetValue(item.WorkItemId, out var findings)
                    ? findings
                    : Array.Empty<ValidationRuleResult>();

                return new RefinementReadinessState(
                    item.WorkItemId,
                    item.WorkItemType,
                    blockingFindings.Count == 0,
                    blockingFindings.Count > 0,
                    blockingFindings);
            })
            .ToArray();
    }

    private static IReadOnlyList<ImplementationReadinessState> BuildImplementationStates(
        BacklogGraph backlogGraph,
        IReadOnlyList<ValidationRuleResult> implementationFindings)
    {
        var findingsByWorkItemId = implementationFindings
            .GroupBy(finding => finding.WorkItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ValidationRuleResult>)group.ToArray());

        return backlogGraph.Items
            .Where(IsImplementationScope)
            .Where(IsActive)
            .OrderBy(item => item.WorkItemId)
            .Select(item =>
            {
                var blockingFindings = findingsByWorkItemId.TryGetValue(item.WorkItemId, out var findings)
                    ? findings
                    : Array.Empty<ValidationRuleResult>();
                var isReady = blockingFindings.Count == 0;

                return new ImplementationReadinessState(
                    item.WorkItemId,
                    item.WorkItemType,
                    new ReadinessScore(isReady ? 100 : 0),
                    isReady,
                    blockingFindings.Any(finding => string.Equals(finding.Rule.SemanticTag, MissingEffortSemanticTag, StringComparison.Ordinal)),
                    blockingFindings);
            })
            .ToArray();
    }

    private static HashSet<int> GetSuppressedImplementationIds(
        BacklogGraph backlogGraph,
        IReadOnlyList<RefinementReadinessState> refinementStates)
    {
        var suppressedIds = new HashSet<int>();
        var queue = new Queue<int>(refinementStates
            .Where(state => state.SuppressesImplementationReadiness)
            .Select(state => state.WorkItemId));

        while (queue.Count > 0)
        {
            var workItemId = queue.Dequeue();
            if (!suppressedIds.Add(workItemId))
            {
                continue;
            }

            foreach (var child in backlogGraph.GetChildren(workItemId))
            {
                queue.Enqueue(child.WorkItemId);
            }
        }

        return suppressedIds;
    }

    private static bool IsActive(WorkItemSnapshot item)
    {
        return item.StateClassification is not StateClassification.Done and not StateClassification.Removed;
    }

    private static bool IsRefinementScope(WorkItemSnapshot item)
    {
        return string.Equals(item.WorkItemType, BacklogWorkItemTypes.Epic, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.WorkItemType, BacklogWorkItemTypes.Feature, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImplementationScope(WorkItemSnapshot item)
    {
        return string.Equals(item.WorkItemType, BacklogWorkItemTypes.Feature, StringComparison.OrdinalIgnoreCase) ||
               BacklogWorkItemTypes.PbiTypes.Contains(item.WorkItemType, StringComparer.OrdinalIgnoreCase);
    }
}
