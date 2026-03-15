namespace PoTool.Core.Domain.BacklogQuality.Rules;

/// <summary>
/// Canonical backlog-quality rule contract.
/// </summary>
public interface IBacklogQualityRule
{
    /// <summary>
    /// Gets the embedded rule metadata.
    /// </summary>
    RuleMetadata Metadata { get; }

    /// <summary>
    /// Evaluates the rule against the canonical backlog graph.
    /// </summary>
    IReadOnlyList<Models.ValidationRuleResult> Evaluate(Models.BacklogGraph backlogGraph);
}
