namespace PoTool.Client.Models;

/// <summary>
/// Type of dependency link between work items.
/// Client-side copy that matches PoTool.Core.WorkItems.DependencyLinkType.
/// </summary>
public enum DependencyLinkType
{
    RelatedTo = 0,
    DependsOn = 1,
    Blocks = 2,
    Parent = 3,
    Child = 4
}
