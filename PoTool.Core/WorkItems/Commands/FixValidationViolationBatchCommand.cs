using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to fix multiple validation violations in batch.
/// Provides automated fix suggestions and applies them to TFS.
/// </summary>
public sealed record FixValidationViolationBatchCommand(
    IReadOnlyList<FixValidationViolationDto> Fixes
) : ICommand<FixValidationViolationResultDto>;
