using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Client.Models;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.DataState;
using PoTool.Shared.Health;
using PoTool.Shared.Metrics;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;
using SharedFilterTimeSelectionDto = PoTool.Shared.Metrics.FilterTimeSelectionDto;
using SharedFilterTimeSelectionModeDto = PoTool.Shared.Metrics.FilterTimeSelectionModeDto;
using SharedSprintFilterContextDto = PoTool.Shared.Metrics.SprintFilterContextDto;
using SharedValidationRuleGroupDto = PoTool.Shared.WorkItems.ValidationRuleGroupDto;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class WorkspaceSignalServiceTests
{
    private static readonly HttpClient TestHttpClient = new()
    {
        BaseAddress = new Uri("http://localhost")
    };

    [TestMethod]
    public void SelectHealthSignal_PicksHighestPriorityStructuralMismatch()
    {
        var summary = new ValidationTriageSummaryDto(
            new ValidationCategoryTriageDto(
                "SI",
                "Structural Integrity",
                6,
                [new SharedValidationRuleGroupDto("SI-1", 4), new SharedValidationRuleGroupDto("SI-3", 2)]),
            new ValidationCategoryTriageDto("RR", "Refinement Readiness", 1, []),
            new ValidationCategoryTriageDto("RC", "Refinement Completeness", 3, []),
            new ValidationCategoryTriageDto("EFF", "Missing Effort", 5, []));

        var result = WorkspaceSignalService.SelectHealthSignal(summary);

        Assert.AreEqual("Investigate 4 parent-child state mismatches", result);
    }

    [TestMethod]
    public void SelectHealthSignal_WithNoIssues_ReturnsHealthyFallback()
    {
        var summary = new ValidationTriageSummaryDto(
            new ValidationCategoryTriageDto("SI", "Structural Integrity", 0, []),
            new ValidationCategoryTriageDto("RR", "Refinement Readiness", 0, []),
            new ValidationCategoryTriageDto("RC", "Refinement Completeness", 0, []),
            new ValidationCategoryTriageDto("EFF", "Missing Effort", 0, []));

        var result = WorkspaceSignalService.SelectHealthSignal(summary);

        Assert.AreEqual("Confirm backlog is healthy", result);
    }

    [TestMethod]
    public void SelectDeliverySignal_PicksLargestImpactWhenPriorityMatches()
    {
        var nowUtc = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var contexts = new[]
        {
            CreateDeliveryContext(sprintId: 1, sprintEndUtc: nowUtc.AddDays(7), addedDuringSprintCount: 2, initialScopeCount: 20),
            CreateDeliveryContext(sprintId: 2, sprintEndUtc: nowUtc.AddDays(7), addedDuringSprintCount: 5, initialScopeCount: 40)
        };

        var result = WorkspaceSignalService.SelectDeliverySignal(contexts, nowUtc);

        Assert.AreEqual("5 PBIs added mid-sprint", result);
    }

    [TestMethod]
    public void SelectDeliverySignal_PrefersUnfinishedWorkNearSprintEnd()
    {
        var nowUtc = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var contexts = new[]
        {
            CreateDeliveryContext(sprintId: 1, sprintEndUtc: nowUtc.AddDays(1), unfinishedCount: 4, addedDuringSprintCount: 6, initialScopeCount: 12)
        };

        var result = WorkspaceSignalService.SelectDeliverySignal(contexts, nowUtc);

        Assert.AreEqual("4 PBIs may spill over this sprint", result);
    }

    [TestMethod]
    public void SelectDeliverySignal_WithNoSignals_ReturnsNeutralInvestigationCue()
    {
        var result = WorkspaceSignalService.SelectDeliverySignal(contexts: [], new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero));

        Assert.AreEqual("Confirm delivery is on track", result);
    }

    [TestMethod]
    public void SelectTrendsSignal_PrefersBugRateIncreaseOverLowerPrioritySignals()
    {
        var sprintTrends = new GetSprintTrendMetricsResponse
        {
            Success = true,
            Metrics =
            [
                CreateSprintTrendMetric(2, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), bugsCreated: 13, completedPbis: 8),
                CreateSprintTrendMetric(1, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), bugsCreated: 10, completedPbis: 12)
            ]
        };
        var prTrends = new GetPrSprintTrendsResponse
        {
            Success = true,
            Sprints =
            [
                CreatePrTrendMetric(2, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), medianTimeToMergeHours: 15),
                CreatePrTrendMetric(1, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), medianTimeToMergeHours: 10)
            ]
        };

        var result = WorkspaceSignalService.SelectTrendsSignal(sprintTrends, prTrends);

        Assert.AreEqual("Bug spike detected (+30%)", result);
    }

    [TestMethod]
    public void SelectTrendsSignal_WithNoTrendChanges_ReturnsNeutralInvestigationCue()
    {
        var result = WorkspaceSignalService.SelectTrendsSignal(sprintTrends: null, pullRequestTrends: null);

        Assert.AreEqual("Confirm trends are stable", result);
    }

    [TestMethod]
    public void SelectPlanningSignal_ReportsNoReadyFeaturesBeforeOtherSignals()
    {
        var states = new[]
        {
            new ProductBacklogStateDto
            {
                ProductId = 1,
                Epics =
                [
                    new EpicRefinementDto
                    {
                        TfsId = 100,
                        Title = "Epic",
                        Score = 50,
                        HasDescription = true,
                        Features =
                        [
                            new FeatureRefinementDto
                            {
                                TfsId = 200,
                                Title = "Feature",
                                Score = 75,
                                OwnerState = FeatureOwnerState.Team,
                                Pbis =
                                [
                                    new PbiReadinessDto { TfsId = 300, Score = 75, Title = "PBI", Effort = 5 }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var result = WorkspaceSignalService.SelectPlanningSignal(states, capacityCalibration: null);

        Assert.AreEqual("No features are ready for sprint", result);
    }

    [TestMethod]
    public void SelectPlanningSignal_UsesCapacityUnderfillBeforeNeutralFallback()
    {
        var states = new[]
        {
            CreatePlanningState(
                featureCount: 2,
                readyPbiCount: 4,
                readyEfforts: [3, 3, 3, 3])
        };
        var capacity = new CapacityCalibrationDto([], MedianVelocity: 20, P25Velocity: 18, P75Velocity: 22, MedianPredictability: 0.9, OutlierSprintNames: []);

        var result = WorkspaceSignalService.SelectPlanningSignal(states, capacity);

        Assert.AreEqual("Ready work is short by 8 pts", result);
    }

    [TestMethod]
    public void SelectPlanningSignal_ReturnsPlanningReadyWhenEnoughReadyWorkExists()
    {
        var states = new[]
        {
            CreatePlanningState(
                featureCount: 2,
                readyPbiCount: 5,
                readyEfforts: [5, 5, 5, 5, 5])
        };
        var capacity = new CapacityCalibrationDto([], MedianVelocity: 20, P25Velocity: 18, P75Velocity: 22, MedianPredictability: 0.9, OutlierSprintNames: []);

        var result = WorkspaceSignalService.SelectPlanningSignal(states, capacity);

        Assert.AreEqual("Confirm planning is healthy", result);
    }

    [TestMethod]
    public async Task GetHealthSignalAsync_WithNoScopedProducts_ReturnsNeutralSignal()
    {
        var service = CreateService();

        var result = await service.GetHealthSignalAsync(CreateProducts(), selectedProductId: 999);

        Assert.AreEqual(WorkspaceSignalSet.Neutral.Health, result);
    }

    [TestMethod]
    public async Task GetDeliverySignalAsync_WithNoScopedProducts_ReturnsNeutralSignal()
    {
        var service = CreateService();

        var result = await service.GetDeliverySignalAsync(42, CreateProducts(), selectedProductId: 999);

        Assert.AreEqual(WorkspaceSignalSet.Neutral.Delivery, result);
    }

    [TestMethod]
    public async Task GetTrendsSignalAsync_WithNoScopedProducts_ReturnsNeutralSignal()
    {
        var service = CreateService();

        var result = await service.GetTrendsSignalAsync(42, CreateProducts(), selectedProductId: 999);

        Assert.AreEqual(WorkspaceSignalSet.Neutral.Trends, result);
    }

    [TestMethod]
    public async Task GetPlanningSignalAsync_WithNoScopedProducts_ReturnsNeutralSignal()
    {
        var service = CreateService();

        var result = await service.GetPlanningSignalAsync(42, CreateProducts(), selectedProductId: 999);

        Assert.AreEqual(WorkspaceSignalSet.Neutral.Planning, result);
    }

    [TestMethod]
    public async Task GetTrendsSignalAsync_PreservesFilterMetadataForDiagnostics()
    {
        var metricsClient = new Mock<IMetricsClient>();
        var pullRequestsClient = new Mock<IPullRequestsClient>();
        var workItemsClient = new Mock<IWorkItemsClient>();
        var sprintsClient = new Mock<ISprintsClient>();

        sprintsClient
            .Setup(client => client.GetSprintsForTeamAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SprintDto
                {
                    Id = 2,
                    TeamId = 10,
                    Name = "Sprint 2",
                    Path = "Team\\Sprint 2",
                    StartUtc = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    EndUtc = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero)
                },
                new SprintDto
                {
                    Id = 1,
                    TeamId = 10,
                    Name = "Sprint 1",
                    Path = "Team\\Sprint 1",
                    StartUtc = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                    EndUtc = new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero)
                }
            ]);

        metricsClient
            .Setup(client => client.GetSprintTrendMetricsAsync(
                42,
                It.IsAny<IEnumerable<int>>(),
                null,
                false,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataStateResponseDtoOfSprintQueryResponseDtoOfGetSprintTrendMetricsResponse
            {
                State = DataStateDto.Available,
                Data = new SprintQueryResponseDtoOfGetSprintTrendMetricsResponse
                {
                    Data = new GetSprintTrendMetricsResponse { Success = true, Metrics = [], ProductAnalytics = [] },
                    RequestedFilter = CreateSprintFilter([100], [10]),
                    EffectiveFilter = CreateSprintFilter([], [10], isAllProducts: true),
                    InvalidFields = ["productIds"],
                    ValidationMessages = [new FilterValidationIssueDto { Field = "productIds", Message = "Product scope was normalized." }]
                }
            });

        pullRequestsClient
            .Setup(client => client.GetSprintTrendsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataStateResponseDtoOfPullRequestQueryResponseDtoOfGetPrSprintTrendsResponse
            {
                State = DataStateDto.Available,
                Data = new PullRequestQueryResponseDtoOfGetPrSprintTrendsResponse
                {
                    Data = new GetPrSprintTrendsResponse { Success = true, Sprints = [] },
                    RequestedFilter = new PoTool.Shared.PullRequests.PullRequestFilterContextDto
                    {
                        ProductIds = new FilterSelectionDto<int> { IsAll = false, Values = [100] },
                        TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                        RepositoryNames = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        CreatedBys = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        Statuses = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        Time = new SharedFilterTimeSelectionDto { Mode = SharedFilterTimeSelectionModeDto.MultiSprint, SprintIds = [1, 2] }
                    },
                    EffectiveFilter = new PoTool.Shared.PullRequests.PullRequestFilterContextDto
                    {
                        ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                        TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                        RepositoryNames = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        CreatedBys = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        Statuses = new FilterSelectionDto<string> { IsAll = true, Values = [] },
                        Time = new SharedFilterTimeSelectionDto { Mode = SharedFilterTimeSelectionModeDto.MultiSprint, SprintIds = [1, 2] }
                    },
                    InvalidFields = [],
                    ValidationMessages = []
                }
            });

        var sprintService = new SprintService(sprintsClient.Object);
        var workItemService = new WorkItemService(
            workItemsClient.Object,
            TestHttpClient,
            new WorkItemLoadCoordinatorService(NullLogger<WorkItemLoadCoordinatorService>.Instance));
        var service = new WorkspaceSignalService(
            metricsClient.Object,
            pullRequestsClient.Object,
            workItemsClient.Object,
            sprintService,
            workItemService);

        await service.GetTrendsSignalAsync(42, CreateProducts(), selectedProductId: null);

        Assert.HasCount(2, service.LatestTrendFilterMetadata);
        Assert.IsTrue(service.LatestTrendFilterMetadata.Any(metadata => metadata.Kind == CanonicalFilterKind.Sprint));
        Assert.IsTrue(service.LatestTrendFilterMetadata.Any(metadata => metadata.Kind == CanonicalFilterKind.PullRequest));
    }

    private static WorkspaceSignalService CreateService()
    {
        var metricsClient = new Mock<IMetricsClient>();
        var pullRequestsClient = new Mock<IPullRequestsClient>();
        var workItemsClient = new Mock<IWorkItemsClient>();
        var sprintsClient = new Mock<ISprintsClient>();
        var sprintService = new SprintService(sprintsClient.Object);
        var workItemService = new WorkItemService(
            workItemsClient.Object,
            TestHttpClient,
            new WorkItemLoadCoordinatorService(NullLogger<WorkItemLoadCoordinatorService>.Instance));

        return new WorkspaceSignalService(
            metricsClient.Object,
            pullRequestsClient.Object,
            workItemsClient.Object,
            sprintService,
            workItemService);
    }

    private static IReadOnlyCollection<ProductDto> CreateProducts()
    {
        return
        [
            new ProductDto
            {
                Id = 1,
                Name = "Product A",
                TeamIds = [10]
            }
        ];
    }

    private static DeliverySignalContext CreateDeliveryContext(
        int sprintId,
        DateTimeOffset sprintEndUtc,
        int unfinishedCount = 0,
        int addedDuringSprintCount = 0,
        int initialScopeCount = 0,
        int blockedCount = 0,
        int missingEffortCount = 0)
    {
        return new DeliverySignalContext(
            new SprintDto
            {
                Id = sprintId,
                TeamId = 10 + sprintId,
                Path = $"Team\\Sprint {sprintId}",
                Name = $"Sprint {sprintId}",
                StartUtc = sprintEndUtc.AddDays(-14),
                EndUtc = sprintEndUtc,
                TimeFrame = "current",
                LastSyncedUtc = sprintEndUtc
            },
            new SprintExecutionDto
            {
                SprintId = sprintId,
                SprintName = $"Sprint {sprintId}",
                StartUtc = sprintEndUtc.AddDays(-14),
                EndUtc = sprintEndUtc,
                HasData = true,
                Summary = new SprintExecutionSummaryDto
                {
                    InitialScopeCount = initialScopeCount,
                    AddedDuringSprintCount = addedDuringSprintCount,
                    UnfinishedCount = unfinishedCount
                },
                CompletedPbis = [],
                UnfinishedPbis = [],
                AddedDuringSprint = [],
                RemovedDuringSprint = [],
                SpilloverPbis = [],
                StarvedPbis = []
            },
            new BacklogHealthDto
            {
                IterationPath = $"Team\\Sprint {sprintId}",
                SprintName = $"Sprint {sprintId}",
                TotalWorkItems = initialScopeCount,
                WorkItemsWithoutEffort = missingEffortCount,
                WorkItemsInProgressWithoutEffort = 0,
                ParentProgressIssues = 0,
                BlockedItems = blockedCount,
                InProgressAtIterationEnd = unfinishedCount,
                IterationStart = sprintEndUtc.AddDays(-14),
                IterationEnd = sprintEndUtc,
                ValidationIssues = []
            },
            []);
    }

    private static SharedSprintFilterContextDto CreateSprintFilter(IReadOnlyList<int> productIds, IReadOnlyList<int> teamIds, bool isAllProducts = false)
        => new()
        {
            ProductIds = new FilterSelectionDto<int> { IsAll = isAllProducts, Values = productIds },
            TeamIds = new FilterSelectionDto<int> { IsAll = false, Values = teamIds },
            AreaPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
            IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = [] },
            Time = new SharedFilterTimeSelectionDto { Mode = SharedFilterTimeSelectionModeDto.MultiSprint, SprintIds = [1, 2] }
        };

    private static SprintTrendMetricsDto CreateSprintTrendMetric(
        int sprintId,
        DateTimeOffset startUtc,
        int bugsCreated,
        int completedPbis)
    {
        return new SprintTrendMetricsDto
        {
            SprintId = sprintId,
            SprintName = $"Sprint {sprintId}",
            StartUtc = startUtc,
            EndUtc = startUtc.AddDays(14),
            ProductMetrics = [],
            TotalPlannedCount = 0,
            TotalPlannedEffort = 0,
            TotalWorkedCount = 0,
            TotalWorkedEffort = 0,
            TotalBugsPlannedCount = 0,
            TotalBugsWorkedCount = 0,
            TotalCompletedPbiCount = completedPbis,
            TotalCompletedPbiEffort = 0,
            TotalProgressionDelta = 0,
            TotalBugsCreatedCount = bugsCreated,
            TotalBugsClosedCount = 0,
            TotalMissingEffortCount = 0,
            IsApproximate = false
        };
    }

    private static PrSprintMetricsDto CreatePrTrendMetric(
        int sprintId,
        DateTimeOffset startUtc,
        double medianTimeToMergeHours)
    {
        return new PrSprintMetricsDto
        {
            SprintId = sprintId,
            SprintName = $"Sprint {sprintId}",
            StartUtc = startUtc,
            EndUtc = startUtc.AddDays(14),
            TotalPrs = 8,
            MedianPrSize = 100,
            PrSizeIsLinesChanged = true,
            MedianTimeToFirstReviewHours = 2,
            MedianTimeToMergeHours = medianTimeToMergeHours,
            P90TimeToMergeHours = medianTimeToMergeHours + 4
        };
    }

    private static ProductBacklogStateDto CreatePlanningState(
        int featureCount,
        int readyPbiCount,
        IReadOnlyList<int> readyEfforts)
    {
        var features = Enumerable.Range(0, featureCount)
            .Select(index => new FeatureRefinementDto
            {
                TfsId = 200 + index,
                Title = $"Feature {index + 1}",
                Score = 100,
                OwnerState = FeatureOwnerState.Ready,
                Pbis =
                [
                    ..Enumerable.Range(0, readyPbiCount / featureCount + (index < readyPbiCount % featureCount ? 1 : 0))
                        .Select(offset =>
                        {
                            var pbiIndex = featureCount == 1 ? offset : featuresAssignedBefore(index, featureCount, readyPbiCount) + offset;
                            return new PbiReadinessDto
                            {
                                TfsId = 300 + pbiIndex,
                                Score = 100,
                                Title = $"PBI {pbiIndex + 1}",
                                Effort = readyEfforts[pbiIndex]
                            };
                        })
                ]
            })
            .ToList();

        return new ProductBacklogStateDto
        {
            ProductId = 1,
            Epics =
            [
                new EpicRefinementDto
                {
                    TfsId = 100,
                    Title = "Epic",
                    Score = 100,
                    HasDescription = true,
                    Features = features
                }
            ]
        };

        static int featuresAssignedBefore(int featureIndex, int featureCount, int readyPbiCount)
        {
            var baseCount = readyPbiCount / featureCount;
            var remainder = readyPbiCount % featureCount;
            return featureIndex * baseCount + Math.Min(featureIndex, remainder);
        }
    }
}
