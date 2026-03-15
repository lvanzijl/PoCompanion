using PoTool.Core.Domain.BacklogQuality.Models;

namespace PoTool.Core.Domain.BacklogQuality.Services;

/// <summary>
/// Derives canonical binary readiness states from backlog readiness scores and direct rule semantics.
/// </summary>
public sealed class ImplementationReadinessService
{
    private const string EpicDescriptionRuleId = "RR-1";
    private const string FeatureDescriptionRuleId = "RR-2";
    private const string EpicMissingChildrenRuleId = "RR-3";
    private const string PbiDescriptionRuleId = "RC-1";
    private const string PbiMissingEffortRuleId = "RC-2";
    private const string FeatureMissingChildrenRuleId = "RC-3";
    private const string MissingEffortSemanticTag = "MissingEffort";

    private readonly RuleCatalog _ruleCatalog;
    private readonly BacklogReadinessService _backlogReadinessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImplementationReadinessService"/> class.
    /// </summary>
    public ImplementationReadinessService()
        : this(new RuleCatalog(), new BacklogReadinessService())
    {
    }

    internal ImplementationReadinessService(
        RuleCatalog ruleCatalog,
        BacklogReadinessService backlogReadinessService)
    {
        _ruleCatalog = ruleCatalog ?? throw new ArgumentNullException(nameof(ruleCatalog));
        _backlogReadinessService = backlogReadinessService ?? throw new ArgumentNullException(nameof(backlogReadinessService));
    }

    /// <summary>
    /// Computes binary readiness for active Epic, Feature, and PBI scope.
    /// </summary>
    public IReadOnlyList<ImplementationReadinessState> Compute(BacklogGraph backlogGraph)
    {
        ArgumentNullException.ThrowIfNull(backlogGraph);

        return _backlogReadinessService.Compute(backlogGraph)
            .Select(score =>
            {
                var blockingFindings = GetBlockingFindings(backlogGraph, score);
                return new ImplementationReadinessState(
                    score.WorkItemId,
                    score.WorkItemType,
                    score.Score,
                    score.Score.IsFullyReady,
                    blockingFindings.Any(finding => string.Equals(finding.Rule.SemanticTag, MissingEffortSemanticTag, StringComparison.Ordinal)),
                    blockingFindings);
            })
            .OrderBy(state => state.WorkItemId)
            .ToArray();
    }

    private IReadOnlyList<ValidationRuleResult> GetBlockingFindings(BacklogGraph backlogGraph, BacklogReadinessScore score)
    {
        var workItem = backlogGraph.GetWorkItem(score.WorkItemId);
        var findings = new List<ValidationRuleResult>();

        if (string.Equals(workItem.WorkItemType, BacklogWorkItemTypes.Epic, StringComparison.OrdinalIgnoreCase))
        {
            if (!HasDescription(workItem))
            {
                findings.Add(CreateFinding(EpicDescriptionRuleId, workItem));
            }

            if (!HasActiveOrDoneChildren(backlogGraph, workItem.WorkItemId, [BacklogWorkItemTypes.Feature]))
            {
                findings.Add(CreateFinding(EpicMissingChildrenRuleId, workItem));
            }
        }
        else if (string.Equals(workItem.WorkItemType, BacklogWorkItemTypes.Feature, StringComparison.OrdinalIgnoreCase))
        {
            if (!HasDescription(workItem))
            {
                findings.Add(CreateFinding(FeatureDescriptionRuleId, workItem));
            }

            if (!HasActiveOrDoneChildren(backlogGraph, workItem.WorkItemId, BacklogWorkItemTypes.PbiTypes))
            {
                findings.Add(CreateFinding(FeatureMissingChildrenRuleId, workItem));
            }
        }
        else if (BacklogWorkItemTypes.PbiTypes.Contains(workItem.WorkItemType, StringComparer.OrdinalIgnoreCase))
        {
            if (!HasDescription(workItem))
            {
                findings.Add(CreateFinding(PbiDescriptionRuleId, workItem));
            }

            if (workItem.Effort is not > 0)
            {
                findings.Add(CreateFinding(PbiMissingEffortRuleId, workItem));
            }
        }

        return findings;
    }

    private ValidationRuleResult CreateFinding(string ruleId, WorkItemSnapshot workItem)
    {
        if (!_ruleCatalog.TryGetRule(ruleId, out var rule) || rule is null)
        {
            throw new InvalidOperationException($"Canonical backlog-quality rule '{ruleId}' is not registered.");
        }

        return new ValidationRuleResult(
            rule.Metadata,
            workItem.WorkItemId,
            workItem.WorkItemType,
            rule.Metadata.Description);
    }

    private static bool HasActiveOrDoneChildren(
        BacklogGraph backlogGraph,
        int parentWorkItemId,
        IReadOnlyList<string> workItemTypes)
    {
        return backlogGraph.GetChildren(parentWorkItemId)
            .Where(child => workItemTypes.Contains(child.WorkItemType, StringComparer.OrdinalIgnoreCase))
            .Any(child => child.StateClassification != PoTool.Core.Domain.Models.StateClassification.Removed);
    }

    private static bool HasDescription(WorkItemSnapshot workItem)
    {
        return !string.IsNullOrWhiteSpace(workItem.Description);
    }
}
