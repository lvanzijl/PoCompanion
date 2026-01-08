namespace PoTool.Shared.WorkItems;

/// <summary>
/// Type of dependency link between work items.
/// </summary>
public enum DependencyLinkType
{
    RelatedTo = 0,
    DependsOn = 1,
    Blocks = 2,
    Parent = 3,
    Child = 4
}
