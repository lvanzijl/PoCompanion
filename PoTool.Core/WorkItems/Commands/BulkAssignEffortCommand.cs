using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to assign effort estimates to multiple work items in bulk.
/// Provides efficient batch update with smart defaults based on suggestions.
/// </summary>
public sealed record BulkAssignEffortCommand(
    IReadOnlyList<BulkEffortAssignmentDto> Assignments
) : ICommand<BulkEffortAssignmentResultDto>;
