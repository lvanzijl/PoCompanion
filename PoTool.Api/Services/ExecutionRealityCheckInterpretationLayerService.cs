using PoTool.Core.Domain.Cdc.ExecutionRealityCheck;

namespace PoTool.Api.Services;

/// <summary>
/// Composes the Phase 23c execution CDC slice with the Phase 24 interpretation layer.
/// </summary>
public interface IExecutionRealityCheckInterpretationLayerService
{
    Task<ExecutionRealityCheckInterpretation> BuildAsync(
        int productOwnerId,
        int anchorSprintId,
        IReadOnlyList<int>? effectiveProductIds = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Composes the Phase 23c execution CDC slice with the Phase 24 interpretation layer.
/// </summary>
public sealed class ExecutionRealityCheckInterpretationLayerService : IExecutionRealityCheckInterpretationLayerService
{
    private readonly ExecutionRealityCheckCdcSliceService _cdcSliceService;
    private readonly IExecutionRealityCheckInterpretationService _interpretationService;

    public ExecutionRealityCheckInterpretationLayerService(
        ExecutionRealityCheckCdcSliceService cdcSliceService,
        IExecutionRealityCheckInterpretationService interpretationService)
    {
        _cdcSliceService = cdcSliceService;
        _interpretationService = interpretationService;
    }

    public async Task<ExecutionRealityCheckInterpretation> BuildAsync(
        int productOwnerId,
        int anchorSprintId,
        IReadOnlyList<int>? effectiveProductIds = null,
        CancellationToken cancellationToken = default)
    {
        var sliceResult = await _cdcSliceService.BuildAsync(
            productOwnerId,
            anchorSprintId,
            effectiveProductIds,
            cancellationToken);

        return _interpretationService.Interpret(sliceResult);
    }
}
