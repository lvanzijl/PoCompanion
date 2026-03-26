using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Controllers;
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
}
