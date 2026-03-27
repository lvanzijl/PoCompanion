using System.ComponentModel.DataAnnotations;
using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Commands;

public sealed record CapturePortfolioSnapshotsCommand(
    [property: Range(1, int.MaxValue)] int ProductOwnerId) : ICommand<PortfolioSnapshotCaptureResultDto>;
