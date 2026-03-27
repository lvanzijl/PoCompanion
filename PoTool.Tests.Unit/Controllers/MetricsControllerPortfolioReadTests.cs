using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Core.Metrics.Commands;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class MetricsControllerPortfolioReadTests
{
    [TestMethod]
    public async Task GetPortfolioProgress_MapsFilteringParametersIntoQuery()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(
                It.Is<GetPortfolioProgressQuery>(query =>
                    query.ProductOwnerId == 7
                    && query.Options!.ProductId == 3
                    && query.Options.ProjectNumber == "PRJ-100"
                    && query.Options.WorkPackage == "WP-2"
                    && query.Options.LifecycleState == PortfolioLifecycleState.Active
                    && query.Options.SortBy == PortfolioReadSortBy.Progress
                    && query.Options.GroupBy == PortfolioReadGroupBy.Project),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioProgressDto
            {
                SnapshotLabel = "Sprint 4",
                SnapshotTimestamp = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
                PortfolioProgress = 55d,
                TotalWeight = 10d,
                TotalItemCount = 1,
                FilteredItemCount = 1,
                GroupBy = PortfolioReadGroupBy.Project,
                SortBy = PortfolioReadSortBy.Progress,
                SortDirection = PortfolioReadSortDirection.Desc,
                Items = Array.Empty<PortfolioSnapshotItemDto>(),
                HasData = true
            });

        var controller = new MetricsController(mediator.Object, NullLogger<MetricsController>.Instance);

        var result = await controller.GetPortfolioProgress(
            7,
            3,
            "PRJ-100",
            "WP-2",
            PortfolioLifecycleState.Active,
            PortfolioReadSortBy.Progress,
            PortfolioReadSortDirection.Desc,
            PortfolioReadGroupBy.Project,
            CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        mediator.VerifyAll();
    }

    [TestMethod]
    public async Task GetPortfolioSnapshots_ReturnsOkResponseFromMediator()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(It.IsAny<GetPortfolioSnapshotsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioSnapshotDto
            {
                SnapshotLabel = "Sprint 4",
                Timestamp = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
                TotalItemCount = 0,
                FilteredItemCount = 0,
                GroupBy = PortfolioReadGroupBy.None,
                SortBy = PortfolioReadSortBy.Default,
                SortDirection = PortfolioReadSortDirection.Desc,
                Items = Array.Empty<PortfolioSnapshotItemDto>(),
                HasData = true
            });

        var controller = new MetricsController(mediator.Object, NullLogger<MetricsController>.Instance);

        var result = await controller.GetPortfolioSnapshots(7, cancellationToken: CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
    }

    [TestMethod]
    public async Task GetPortfolioComparison_ReturnsOkResponseFromMediator()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(It.IsAny<GetPortfolioComparisonQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioComparisonDto
            {
                PreviousSnapshotLabel = "Sprint 3",
                CurrentSnapshotLabel = "Sprint 4",
                PreviousTimestamp = new DateTimeOffset(2026, 2, 25, 0, 0, 0, TimeSpan.Zero),
                CurrentTimestamp = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
                TotalItemCount = 0,
                FilteredItemCount = 0,
                GroupBy = PortfolioReadGroupBy.None,
                SortBy = PortfolioReadSortBy.Default,
                SortDirection = PortfolioReadSortDirection.Desc,
                Items = Array.Empty<PortfolioComparisonItemDto>(),
                HasData = true
            });

        var controller = new MetricsController(mediator.Object, NullLogger<MetricsController>.Instance);

        var result = await controller.GetPortfolioComparison(7, cancellationToken: CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
    }

    [TestMethod]
    public async Task GetPortfolioComparison_MapsHistoricalSelectionParametersIntoQuery()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(
                It.Is<GetPortfolioComparisonQuery>(query =>
                    query.ProductOwnerId == 7
                    && query.Options!.IncludeArchivedSnapshots
                    && query.Options.CompareToSnapshotId == 99
                    && query.Options.RangeStartUtc == new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)
                    && query.Options.RangeEndUtc == new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioComparisonDto
            {
                PreviousSnapshotLabel = "Sprint 1",
                CurrentSnapshotLabel = "Sprint 3",
                PreviousTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                CurrentTimestamp = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
                TotalItemCount = 0,
                FilteredItemCount = 0,
                GroupBy = PortfolioReadGroupBy.None,
                SortBy = PortfolioReadSortBy.Default,
                SortDirection = PortfolioReadSortDirection.Desc,
                Items = Array.Empty<PortfolioComparisonItemDto>(),
                HasData = true
            });

        var controller = new MetricsController(mediator.Object, NullLogger<MetricsController>.Instance);

        var result = await controller.GetPortfolioComparison(
            7,
            rangeStartUtc: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            rangeEndUtc: new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
            includeArchivedSnapshots: true,
            compareToSnapshotId: 99,
            cancellationToken: CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        mediator.VerifyAll();
    }

    [TestMethod]
    public async Task GetPortfolioTrends_ReturnsOkResponseFromMediator()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(It.IsAny<GetPortfolioTrendsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioTrendDto
            {
                Snapshots = Array.Empty<PortfolioHistoricalSnapshotDto>(),
                PortfolioProgressTrend = new PortfolioMetricTrendDto { Points = Array.Empty<PortfolioTrendPointDto>() },
                TotalWeightTrend = new PortfolioMetricTrendDto { Points = Array.Empty<PortfolioTrendPointDto>() },
                Projects = Array.Empty<PortfolioScopedTrendDto>(),
                WorkPackages = Array.Empty<PortfolioScopedTrendDto>(),
                SnapshotCount = 6,
                IncludesArchivedSnapshots = false,
                ArchivedSnapshotsExcludedByDefault = true,
                ArchivedSnapshotsExcludedNotice = false,
                HasData = true
            });

        var controller = new MetricsController(mediator.Object, NullLogger<MetricsController>.Instance);

        var result = await controller.GetPortfolioTrends(7, snapshotCount: 6, cancellationToken: CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
    }

    [TestMethod]
    public async Task GetPortfolioSignals_ReturnsOkResponseFromMediator()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(It.IsAny<GetPortfolioSignalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PortfolioDecisionSignalDto
                {
                    Type = PortfolioDecisionSignalType.ProgressImproving,
                    Tone = PortfolioDecisionSignalTone.Positive,
                    Title = "Portfolio progress is improving",
                    Description = "Progress increased."
                }
            ]);

        var controller = new MetricsController(mediator.Object, NullLogger<MetricsController>.Instance);

        var result = await controller.GetPortfolioSignals(7, snapshotCount: 6, cancellationToken: CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
    }

    [TestMethod]
    public void PortfolioReadEndpoints_RemainGetOnly_AndCaptureEndpointIsExplicitPost()
    {
        var metricsMethods = typeof(MetricsController)
            .GetMethods()
            .Where(method => method.Name is nameof(MetricsController.GetPortfolioProgress)
                or nameof(MetricsController.GetPortfolioSnapshots)
                or nameof(MetricsController.GetPortfolioComparison)
                or nameof(MetricsController.GetPortfolioTrends)
                or nameof(MetricsController.GetPortfolioSignals))
            .ToArray();

        Assert.IsTrue(metricsMethods.All(method => method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false).Length == 1));
        Assert.IsTrue(metricsMethods.All(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: false).Length == 0));

        var captureMethod = typeof(PortfolioSnapshotsController).GetMethod(nameof(PortfolioSnapshotsController.Capture));
        Assert.IsNotNull(captureMethod);
        Assert.HasCount(1, captureMethod.GetCustomAttributes(typeof(HttpPostAttribute), inherit: false));
        Assert.IsEmpty(captureMethod.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false));
    }

    [TestMethod]
    public async Task CapturePortfolioSnapshots_PostsExplicitCommandToMediator()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(
                It.Is<CapturePortfolioSnapshotsCommand>(command => command.ProductOwnerId == 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioSnapshotCaptureResultDto(1, 2, 2, 0));

        var controller = new PortfolioSnapshotsController(
            mediator.Object,
            NullLogger<PortfolioSnapshotsController>.Instance);

        var result = await controller.Capture(
            new CapturePortfolioSnapshotsCommand(7),
            CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        mediator.VerifyAll();
    }
}
