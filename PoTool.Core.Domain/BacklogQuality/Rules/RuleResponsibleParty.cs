namespace PoTool.Core.Domain.BacklogQuality.Rules;

/// <summary>
/// Canonical party responsible for resolving a backlog-quality finding.
/// </summary>
public enum RuleResponsibleParty
{
    ProductOwner = 0,
    Team = 1,
    Process = 2
}
