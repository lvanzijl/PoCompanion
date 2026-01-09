namespace PoTool.Shared.WorkItems;

/// <summary>
/// Represents a validation issue for a work item.
/// </summary>
public sealed record ValidationIssue(
    string Severity,
    string Message
);
