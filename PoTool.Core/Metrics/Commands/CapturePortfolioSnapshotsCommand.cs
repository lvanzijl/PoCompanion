using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Commands;

public sealed record CapturePortfolioSnapshotsCommand(
    int ProductOwnerId) : ICommand<PortfolioSnapshotCaptureResultDto>;
