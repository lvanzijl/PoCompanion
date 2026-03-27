using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Commands;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

public sealed class CapturePortfolioSnapshotsCommandHandler
    : ICommandHandler<CapturePortfolioSnapshotsCommand, PortfolioSnapshotCaptureResultDto>
{
    private readonly IPortfolioSnapshotCaptureOrchestrator _orchestrator;

    public CapturePortfolioSnapshotsCommandHandler(IPortfolioSnapshotCaptureOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async ValueTask<PortfolioSnapshotCaptureResultDto> Handle(
        CapturePortfolioSnapshotsCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _orchestrator.CaptureLatestAsync(command.ProductOwnerId, cancellationToken);
        return new PortfolioSnapshotCaptureResultDto(
            result.ProductCount,
            result.SourceCount,
            result.CreatedSnapshotCount,
            result.ExistingSnapshotCount);
    }
}
