using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;
using PoTool.Shared.PullRequests;
using PoTool.Tests.Unit.TestSupport;
using SharedFilterTimeSelectionDto = PoTool.Shared.Metrics.FilterTimeSelectionDto;
using SharedFilterTimeSelectionModeDto = PoTool.Shared.Metrics.FilterTimeSelectionModeDto;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class TrendFilterRangeMappingTests
{
    [TestMethod]
    public async Task WorkspaceSignalService_GetTrendsSignalAsync_UsesRequestedRangeSprintIds()
    {
        var metricsClient = new Mock<IMetricsClient>(MockBehavior.Strict);
        var pullRequestsClient = new Mock<IPullRequestsClient>(MockBehavior.Strict);
        var workItemsClient = new Mock<IWorkItemsClient>(MockBehavior.Strict);
        var sprintsClient = new Mock<ISprintsClient>(MockBehavior.Strict);

        sprintsClient
            .Setup(client => client.GetSprintsForTeamAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateSprint(45, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                CreateSprint(46, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero)),
                CreateSprint(47, new DateTimeOffset(2026, 1, 29, 0, 0, 0, TimeSpan.Zero)),
                CreateSprint(48, new DateTimeOffset(2026, 2, 12, 0, 0, 0, TimeSpan.Zero)),
                CreateSprint(49, new DateTimeOffset(2026, 2, 26, 0, 0, 0, TimeSpan.Zero))
            });

        metricsClient
            .Setup(client => client.GetSprintTrendMetricsAsync(
                42,
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 45, 46, 47, 48, 49 })),
                null,
                false,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSprintTrendResponse());

        pullRequestsClient
            .Setup(client => client.GetSprintTrendsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 45, 46, 47, 48, 49 })),
                "1",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePrTrendResponse());

        var sprintService = new SprintService(sprintsClient.Object);
        var workItemService = new WorkItemService(
            workItemsClient.Object,
            new HttpClient { BaseAddress = new Uri("http://localhost") },
            new WorkItemLoadCoordinatorService(NullLogger<WorkItemLoadCoordinatorService>.Instance));
        var service = new WorkspaceSignalService(
            metricsClient.Object,
            pullRequestsClient.Object,
            sprintService,
            workItemService,
            NullLogger<WorkspaceSignalService>.Instance);

        var result = await service.GetTrendsSignalAsync(
            42,
            [new ProductDto { Id = 1, Name = "Product A", TeamIds = [10] }],
            new FilterState(Array.Empty<int>(), Array.Empty<string>(), 10, new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: 45, EndSprintId: 49)));

        Assert.AreEqual(DataStateResultStatus.Ready, result.Status);
        metricsClient.VerifyAll();
        pullRequestsClient.VerifyAll();
    }

    [TestMethod]
    public async Task SprintFilterResolutionService_EmptyExplicitMultiSprintInput_IsInvalid()
    {
        await using var context = CreateContext();
        var service = new SprintFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<SprintFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new SprintFilterBoundaryRequest(SprintIds: Array.Empty<int>()),
            "TrendBoundary",
            CancellationToken.None);

        Assert.IsFalse(resolution.Validation.IsValid);
        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(PoTool.Core.Metrics.Filters.SprintFilterContext.Time));
    }

    [TestMethod]
    public async Task DeliveryFilterResolutionService_EmptyExplicitMultiSprintInput_IsInvalid()
    {
        await using var context = CreateContext();
        var service = new DeliveryFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<DeliveryFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new DeliveryFilterBoundaryRequest(SprintIds: Array.Empty<int>()),
            "TrendBoundary",
            CancellationToken.None);

        Assert.IsFalse(resolution.Validation.IsValid);
        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(PoTool.Core.Delivery.Filters.DeliveryFilterContext.Time));
    }

    [TestMethod]
    public async Task PullRequestFilterResolutionService_EmptyExplicitMultiSprintInput_IsInvalid()
    {
        await using var context = CreateContext();
        var service = new PullRequestFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<PullRequestFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PullRequestFilterBoundaryRequest(SprintIds: Array.Empty<int>()),
            "TrendBoundary",
            CancellationToken.None);

        Assert.IsFalse(resolution.Validation.IsValid);
        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(PoTool.Core.PullRequests.Filters.PullRequestFilterContext.Time));
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"TrendFilterRangeMappingTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }

    private static SprintDto CreateSprint(int id, DateTimeOffset startUtc)
        => new()
        {
            Id = id,
            TeamId = 10,
            Name = $"Sprint {id}",
            Path = $"Team\\Sprint {id}",
            StartUtc = startUtc,
            EndUtc = startUtc.AddDays(13)
        };

    private static DataStateResponseDtoOfSprintQueryResponseDtoOfGetSprintTrendMetricsResponse CreateSprintTrendResponse()
        => new()
        {
            State = DataStateDto.Available,
            Data = new SprintQueryResponseDtoOfGetSprintTrendMetricsResponse
            {
                Data = new GetSprintTrendMetricsResponse { Success = true, Metrics = [], ProductAnalytics = [] },
                RequestedFilter = new SprintFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    AreaPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    Time = new SharedFilterTimeSelectionDto { Mode = SharedFilterTimeSelectionModeDto.MultiSprint, SprintIds = [45, 46, 47, 48, 49] }
                },
                EffectiveFilter = new SprintFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    AreaPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    Time = new SharedFilterTimeSelectionDto { Mode = SharedFilterTimeSelectionModeDto.MultiSprint, SprintIds = [45, 46, 47, 48, 49] }
                },
                InvalidFields = [],
                ValidationMessages = []
            }
        };

    private static DataStateResponseDtoOfPullRequestQueryResponseDtoOfGetPrSprintTrendsResponse CreatePrTrendResponse()
        => new()
        {
            State = DataStateDto.Available,
            Data = new PullRequestQueryResponseDtoOfGetPrSprintTrendsResponse
            {
                Data = new GetPrSprintTrendsResponse { Success = true, Sprints = [] },
                RequestedFilter = new PoTool.Shared.PullRequests.PullRequestFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    RepositoryNames = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    CreatedBys = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    Statuses = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    Time = new SharedFilterTimeSelectionDto { Mode = SharedFilterTimeSelectionModeDto.MultiSprint, SprintIds = [45, 46, 47, 48, 49] }
                },
                EffectiveFilter = new PoTool.Shared.PullRequests.PullRequestFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    RepositoryNames = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    CreatedBys = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    Statuses = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                    Time = new SharedFilterTimeSelectionDto { Mode = SharedFilterTimeSelectionModeDto.MultiSprint, SprintIds = [45, 46, 47, 48, 49] }
                },
                InvalidFields = [],
                ValidationMessages = []
            }
        };
}
