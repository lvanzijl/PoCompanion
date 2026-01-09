namespace PoTool.Shared.WorkItems;

/// <summary>
/// DTO representing a dependency graph of work items.
/// Shows relationships and dependency chains.
/// </summary>
public sealed record DependencyGraphDto(
    IReadOnlyList<DependencyNode> Nodes,
    IReadOnlyList<DependencyLink> Links,
    IReadOnlyList<DependencyChain> CriticalPaths,
    IReadOnlyList<int> BlockedWorkItemIds,
    IReadOnlyList<CircularDependency> CircularDependencies,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Represents a circular dependency cycle in the work item graph.
/// </summary>
public sealed record CircularDependency(
    IReadOnlyList<int> CycleWorkItemIds,
    string Description
);

/// <summary>
/// A node in the dependency graph representing a work item.
/// </summary>
public sealed record DependencyNode(
    int WorkItemId,
    string Title,
    string Type,
    string State,
    int? Effort,
    int DependencyCount,
    int DependentCount,
    bool IsBlocking
);

/// <summary>
/// A link between two work items in the dependency graph.
/// </summary>
public sealed record DependencyLink(
    int SourceWorkItemId,
    int TargetWorkItemId,
    DependencyLinkType LinkType,
    string Description
);

/// <summary>
/// A chain of dependencies forming a critical path.
/// </summary>
public sealed record DependencyChain(
    IReadOnlyList<int> WorkItemIds,
    int TotalEffort,
    int ChainLength,
    DependencyChainRisk RiskLevel
);

/// <summary>
/// Type of dependency link between work items.
/// </summary>
public enum DependencyLinkType
{
    RelatedTo,
    DependsOn,
    Blocks,
    Parent,
    Child
}

/// <summary>
/// Risk level for a dependency chain.
/// </summary>
public enum DependencyChainRisk
{
    Low,
    Medium,
    High,
    Critical
}
