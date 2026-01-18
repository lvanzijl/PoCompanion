namespace PoTool.Shared.Settings;

/// <summary>
/// Represents a work item type definition from TFS with its available states.
/// </summary>
public record WorkItemTypeDefinitionDto
{
    /// <summary>
    /// The work item type name (e.g., "Epic", "Feature", "Product Backlog Item").
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// The list of valid state names for this work item type.
    /// </summary>
    public required IReadOnlyList<string> States { get; init; }
}
