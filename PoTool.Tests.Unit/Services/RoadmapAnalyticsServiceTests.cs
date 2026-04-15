using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.DataState;
using PoTool.Shared.Health;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class RoadmapAnalyticsServiceTests
{
    private Mock<IMetricsClient> _metricsClientMock = null!;
    private Mock<IWorkItemsClient> _workItemsClientMock = null!;
    private RoadmapAnalyticsService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _metricsClientMock = new Mock<IMetricsClient>();
        _workItemsClientMock = new Mock<IWorkItemsClient>();
        _service = new RoadmapAnalyticsService(_metricsClientMock.Object, _workItemsClientMock.Object);
    }

    #region ComputeLocalAnalytics

    [TestMethod]
    public void ComputeLocalAnalytics_EmptyWorkItems_ReturnsZeroes()
    {
        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(1, Array.Empty<WorkItemDto>());

        Assert.AreEqual(0, result.TotalStoryPoints);
        Assert.AreEqual(0, result.DeliveredStoryPoints);
        Assert.AreEqual(0, result.RemainingStoryPoints);
        Assert.AreEqual(0, result.PbiCount);
        Assert.IsNull(result.EpicAgeDays);
        Assert.IsNull(result.LastActivityDays);
    }

    [TestMethod]
    public void ComputeLocalAnalytics_EpicWithPBIs_SumsActiveEffort()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", parentId: null, effort: null),
            CreateWorkItem(201, "Product Backlog Item", "Active", parentId: 100, effort: 5),
            CreateWorkItem(202, "Product Backlog Item", "Active", parentId: 100, effort: 8),
            CreateWorkItem(203, "Product Backlog Item", "Closed", parentId: 100, effort: 3),
        };

        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(100, workItems);

        Assert.AreEqual(16, result.TotalStoryPoints); // 5 + 8 + 3 (all PBIs)
        Assert.AreEqual(13, result.RemainingStoryPoints); // 5 + 8 (active only)
        Assert.AreEqual(3, result.DeliveredStoryPoints); // 3 (Closed)
        Assert.AreEqual(3, result.PbiCount); // all PBIs including Closed
    }

    [TestMethod]
    public void ComputeLocalAnalytics_EpicWithBugs_IncludesBugsInCount()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", parentId: null, effort: null),
            CreateWorkItem(201, "Product Backlog Item", "Active", parentId: 100, effort: 5),
            CreateWorkItem(202, "Bug", "Active", parentId: 100, effort: 3),
        };

        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(100, workItems);

        Assert.AreEqual(8, result.TotalStoryPoints);
        Assert.AreEqual(2, result.PbiCount); // PBI + Bug
    }

    [TestMethod]
    public void ComputeLocalAnalytics_NestedHierarchy_FindsAllDescendants()
    {
        // Epic → Feature → PBI
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", parentId: null, effort: null),
            CreateWorkItem(150, "Feature", "Active", parentId: 100, effort: null),
            CreateWorkItem(201, "Product Backlog Item", "Active", parentId: 150, effort: 13),
        };

        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(100, workItems);

        Assert.AreEqual(13, result.TotalStoryPoints);
        Assert.AreEqual(1, result.PbiCount);
    }

    [TestMethod]
    public void ComputeLocalAnalytics_DoneAndRemovedExcludedFromRemainingStoryPoints()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", parentId: null, effort: null),
            CreateWorkItem(201, "Product Backlog Item", "Active", parentId: 100, effort: 10),
            CreateWorkItem(202, "Product Backlog Item", "Done", parentId: 100, effort: 5),
            CreateWorkItem(203, "Product Backlog Item", "Removed", parentId: 100, effort: 3),
        };

        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(100, workItems);

        Assert.AreEqual(18, result.TotalStoryPoints); // 10 + 5 + 3 (all PBIs)
        Assert.AreEqual(10, result.RemainingStoryPoints); // only Active
        Assert.AreEqual(8, result.DeliveredStoryPoints); // Done + Removed: 5 + 3
        Assert.AreEqual(3, result.PbiCount); // all PBIs counted
    }

    [TestMethod]
    public void ComputeLocalAnalytics_EpicWithCreatedDate_CalculatesAge()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", parentId: null, effort: null,
                createdDate: DateTimeOffset.UtcNow.AddDays(-210)),
        };

        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(100, workItems);

        Assert.IsNotNull(result.EpicAgeDays);
        Assert.IsTrue(result.EpicAgeDays >= 209 && result.EpicAgeDays <= 211);
    }

    [TestMethod]
    public void ComputeLocalAnalytics_NoPBIs_ReturnsZeroEffortAndCount()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", parentId: null, effort: null),
            CreateWorkItem(150, "Feature", "Active", parentId: 100, effort: null),
        };

        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(100, workItems);

        Assert.AreEqual(0, result.TotalStoryPoints);
        Assert.AreEqual(0, result.PbiCount);
    }

    [TestMethod]
    public void ComputeLocalAnalytics_PBIsWithoutEffort_CountedButNotSummed()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", parentId: null, effort: null),
            CreateWorkItem(201, "Product Backlog Item", "Active", parentId: 100, effort: null),
            CreateWorkItem(202, "Product Backlog Item", "Active", parentId: 100, effort: 5),
        };

        var result = RoadmapAnalyticsService.ComputeLocalAnalytics(100, workItems);

        Assert.AreEqual(5, result.TotalStoryPoints);
        Assert.AreEqual(2, result.PbiCount);
    }

    #endregion

    #region LoadForecastAsync

    [TestMethod]
    public async Task LoadForecastAsync_SuccessfulForecast_ReturnsForecastAnalytics()
    {
        _metricsClientMock
            .Setup(m => m.GetEpicForecastAsync(100, (int?)5))
            .ReturnsAsync(new DataStateResponseDtoOfEpicCompletionForecastDto
            {
                State = DataStateDto.Available,
                Data = new EpicCompletionForecastDto
                {
                    EpicId = 100,
                    Title = "Test Epic",
                    Type = "Epic",
                    TotalStoryPoints = 50,
                    DoneStoryPoints = 10,
                    RemainingStoryPoints = 40,
                    EstimatedVelocity = 10.0,
                    SprintsRemaining = 4,
                    Confidence = ForecastConfidence.High,
                    ForecastByDate = Array.Empty<SprintForecast>(),
                    AreaPath = "Test",
                    AnalysisTimestamp = DateTimeOffset.UtcNow,
                }
            });

        var result = await _service.LoadForecastAsync(100);

        Assert.IsTrue(result.CanUseData);
        Assert.AreEqual(4, result.Data!.SprintsRemaining);
        Assert.IsTrue(result.Data.ExceedsVelocity); // >3 sprints
        Assert.AreEqual(ForecastConfidence.High, result.Data.Confidence);
    }

    [TestMethod]
    public async Task LoadForecastAsync_EpicWithinVelocity_NotFlagged()
    {
        _metricsClientMock
            .Setup(m => m.GetEpicForecastAsync(100, (int?)5))
            .ReturnsAsync(new DataStateResponseDtoOfEpicCompletionForecastDto
            {
                State = DataStateDto.Available,
                Data = new EpicCompletionForecastDto
                {
                    EpicId = 100,
                    Title = "Small Epic",
                    Type = "Epic",
                    TotalStoryPoints = 20,
                    DoneStoryPoints = 10,
                    RemainingStoryPoints = 10,
                    EstimatedVelocity = 10.0,
                    SprintsRemaining = 1,
                    Confidence = ForecastConfidence.High,
                    ForecastByDate = Array.Empty<SprintForecast>(),
                    AreaPath = "Test",
                    AnalysisTimestamp = DateTimeOffset.UtcNow,
                }
            });

        var result = await _service.LoadForecastAsync(100);

        Assert.IsTrue(result.CanUseData);
        Assert.AreEqual(1, result.Data!.SprintsRemaining);
        Assert.IsFalse(result.Data.ExceedsVelocity);
    }

    [TestMethod]
    public async Task LoadForecastAsync_ApiThrows_ReturnsFailedResult()
    {
        _metricsClientMock
            .Setup(m => m.GetEpicForecastAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("API error"));

        var result = await _service.LoadForecastAsync(100);

        Assert.AreEqual(DataStateResultStatus.Failed, result.Status);
    }

    #endregion

    #region LoadBacklogHealthAsync

    [TestMethod]
    public async Task LoadBacklogHealthAsync_WithHealthData_ReturnsPerEpicHealth()
    {
        _workItemsClientMock
            .Setup(m => m.GetBacklogStateAsync(1))
            .ReturnsAsync(new DataStateResponseDtoOfProductBacklogStateDto
            {
                State = DataStateDto.Available,
                Data = new ProductBacklogStateDto
                {
                    ProductId = 1,
                    Epics = new List<EpicRefinementDto>
                    {
                        new()
                        {
                            TfsId = 100,
                            Title = "Epic 1",
                            Score = 80,
                            HasDescription = true,
                            Features = new List<FeatureRefinementDto>
                            {
                                new()
                                {
                                    TfsId = 150,
                                    Title = "Feature 1",
                                    Score = 80,
                                    OwnerState = FeatureOwnerState.Team,
                                    Pbis = new List<PbiReadinessDto>
                                    {
                                        new() { TfsId = 201, Score = 100 },
                                        new() { TfsId = 202, Score = 75 },
                                    }
                                }
                            }
                        }
                    }
                }
            });

        var result = await _service.LoadBacklogHealthAsync(1);

        Assert.IsTrue(result.CanUseData);
        Assert.HasCount(1, result.Data!);
        Assert.IsTrue(result.Data!.ContainsKey(100));
        Assert.AreEqual(80, result.Data[100].RefinementScore);
        Assert.IsFalse(result.Data[100].HasRefinementBlockers);
        Assert.IsTrue(result.Data[100].HasValidationIssues); // PBI 202 has score < 100
    }

    [TestMethod]
    public async Task LoadBacklogHealthAsync_MissingDescription_FlagsRefinementBlocker()
    {
        _workItemsClientMock
            .Setup(m => m.GetBacklogStateAsync(1))
            .ReturnsAsync(new DataStateResponseDtoOfProductBacklogStateDto
            {
                State = DataStateDto.Available,
                Data = new ProductBacklogStateDto
                {
                    ProductId = 1,
                    Epics = new List<EpicRefinementDto>
                    {
                        new()
                        {
                            TfsId = 100,
                            Title = "Epic Without Description",
                            Score = 0,
                            HasDescription = false,
                            Features = new List<FeatureRefinementDto>()
                        }
                    }
                }
            });

        var result = await _service.LoadBacklogHealthAsync(1);

        Assert.IsTrue(result.CanUseData);
        Assert.IsTrue(result.Data![100].HasRefinementBlockers);
    }

    [TestMethod]
    public async Task LoadBacklogHealthAsync_ApiThrows_ReturnsFailedResult()
    {
        _workItemsClientMock
            .Setup(m => m.GetBacklogStateAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("API error"));

        var result = await _service.LoadBacklogHealthAsync(1);

        Assert.AreEqual(DataStateResultStatus.Failed, result.Status);
    }

    #endregion

    #region LoadDependencySignalsAsync

    [TestMethod]
    public async Task LoadDependencySignalsAsync_WithDependencies_ReturnsAffectedIds()
    {
        _workItemsClientMock
            .Setup(m => m.GetDependencyGraphAsync(It.IsAny<string?>(), It.Is<string?>(s => s == "100,200"), It.IsAny<string?>()))
            .ReturnsAsync(new DataStateResponseDtoOfDependencyGraphDto
            {
                State = DataStateDto.Available,
                Data = new DependencyGraphDto
                {
                    Nodes = new List<DependencyNode>
                    {
                        new() { WorkItemId = 100, DependencyCount = 1, DependentCount = 0 },
                        new() { WorkItemId = 200, DependencyCount = 0, DependentCount = 0 },
                    },
                    Links = new List<DependencyLink>(),
                    CriticalPaths = new List<DependencyChain>(),
                    BlockedWorkItemIds = new List<int>(),
                    CircularDependencies = new List<CircularDependency>(),
                    AnalysisTimestamp = DateTimeOffset.UtcNow,
                }
            });

        var result = await _service.LoadDependencySignalsAsync(new[] { 100, 200 });

        Assert.IsTrue(result.CanUseData);
        Assert.HasCount(1, result.Data!);
        CollectionAssert.Contains(result.Data!.ToList(), 100);
        CollectionAssert.DoesNotContain(result.Data!.ToList(), 200);
    }

    [TestMethod]
    public async Task LoadDependencySignalsAsync_ApiThrows_ReturnsFailedResult()
    {
        _workItemsClientMock
            .Setup(m => m.GetDependencyGraphAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("API error"));

        var result = await _service.LoadDependencySignalsAsync(new[] { 100 });

        Assert.AreEqual(DataStateResultStatus.Failed, result.Status);
    }

    [TestMethod]
    public async Task LoadDependencySignalsAsync_EmptyIds_ReturnsEmptyResult()
    {
        var result = await _service.LoadDependencySignalsAsync(Array.Empty<int>());

        Assert.AreEqual(DataStateResultStatus.Empty, result.Status);
    }

    #endregion

    #region Helpers

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string state,
        int? parentId,
        int? effort,
        DateTimeOffset? createdDate = null)
    {
        return new WorkItemDto
        {
            TfsId = tfsId,
            Type = type,
            Title = $"{type} {tfsId}",
            ParentTfsId = parentId,
            AreaPath = "TestArea",
            IterationPath = "TestIteration",
            State = state,
            RetrievedAt = DateTimeOffset.UtcNow,
            Effort = effort,
            CreatedDate = createdDate,
            BacklogPriority = null,
        };
    }

    #endregion
}
