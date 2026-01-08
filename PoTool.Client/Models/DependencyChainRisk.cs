namespace PoTool.Client.Models;

/// <summary>
/// Risk level for a dependency chain.
/// Client-side copy that matches PoTool.Core.WorkItems.DependencyChainRisk.
/// </summary>
public enum DependencyChainRisk
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
