namespace PoTool.Core.WorkItems;

/// <summary>
/// Represents a validation issue for a work item.
/// </summary>
public sealed record ValidationStatus(
    string Category,
    string Severity,
    string Message,
    string IconName
);
