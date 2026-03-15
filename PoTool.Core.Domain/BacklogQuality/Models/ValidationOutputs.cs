using PoTool.Core.Domain.BacklogQuality.Rules;

namespace PoTool.Core.Domain.BacklogQuality.Models;

/// <summary>
/// Represents one canonical backlog-quality rule finding.
/// </summary>
public record ValidationRuleResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationRuleResult"/> class.
    /// </summary>
    public ValidationRuleResult(RuleMetadata rule, int workItemId, string workItemType, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (workItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workItemId), "Work item ID must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(workItemType))
        {
            throw new ArgumentException("Work item type is required.", nameof(workItemType));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A finding message is required.", nameof(message));
        }

        Rule = rule;
        WorkItemId = workItemId;
        WorkItemType = workItemType;
        Message = message;
    }

    /// <summary>
    /// Gets the canonical rule metadata.
    /// </summary>
    public RuleMetadata Rule { get; }

    /// <summary>
    /// Gets the affected work item identifier.
    /// </summary>
    public int WorkItemId { get; }

    /// <summary>
    /// Gets the affected work item type.
    /// </summary>
    public string WorkItemType { get; }

    /// <summary>
    /// Gets the human-readable message.
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// Specialized structural-integrity finding with ancestor/descendant context.
/// </summary>
public sealed record BacklogIntegrityFinding(
    RuleMetadata Rule,
    int WorkItemId,
    string WorkItemType,
    string Message,
    IReadOnlyList<int> ConflictingDescendantIds)
    : ValidationRuleResult(Rule, WorkItemId, WorkItemType, Message);

/// <summary>
/// Discrete refinement-readiness state for a backlog scope node.
/// </summary>
public sealed record RefinementReadinessState(
    int WorkItemId,
    string WorkItemType,
    bool IsReady,
    bool SuppressesImplementationReadiness,
    IReadOnlyList<ValidationRuleResult> BlockingFindings);

/// <summary>
/// Discrete implementation-readiness state for implementable backlog scope.
/// </summary>
public sealed record ImplementationReadinessState(
    int WorkItemId,
    string WorkItemType,
    ReadinessScore Score,
    bool IsReady,
    bool HasMissingEffort,
    IReadOnlyList<ValidationRuleResult> BlockingFindings);

/// <summary>
/// Aggregate output of backlog-quality validation.
/// </summary>
public sealed record BacklogValidationResult(
    IReadOnlyList<BacklogIntegrityFinding> IntegrityFindings,
    IReadOnlyList<ValidationRuleResult> RuleResults,
    IReadOnlyList<RefinementReadinessState> RefinementStates,
    IReadOnlyList<ImplementationReadinessState> ImplementationStates);
