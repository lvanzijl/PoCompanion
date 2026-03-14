using PoTool.Core.Metrics.Services;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CanonicalStoryPointResolutionServiceTests
{
    private readonly CanonicalStoryPointResolutionService _service = new();

    [TestMethod]
    public void Resolve_UsesStoryPointsWhenPresent()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: 8),
            IsDone: false));

        Assert.AreEqual(8d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Real, result.Source);
    }

    [TestMethod]
    public void Resolve_UsesBusinessValueFallbackWhenStoryPointsAreMissing()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: null, businessValue: 13),
            IsDone: false));

        Assert.AreEqual(13d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Fallback, result.Source);
    }

    [TestMethod]
    public void Resolve_ReturnsMissingWhenNoEstimateExists()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: null, businessValue: null),
            IsDone: false));

        Assert.IsNull(result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Missing, result.Source);
    }

    [TestMethod]
    public void Resolve_TreatsZeroStoryPointsOnDoneItemAsValidRealEstimate()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: 0, businessValue: 5),
            IsDone: true));

        Assert.AreEqual(0d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Real, result.Source);
    }

    [TestMethod]
    public void Resolve_TreatsZeroStoryPointsOnNonDoneItemAsMissing()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: 0, businessValue: null),
            IsDone: false));

        Assert.IsNull(result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Missing, result.Source);
    }

    [TestMethod]
    public void Resolve_DerivesMissingEstimateFromSameFeatureSiblingAverage()
    {
        var target = CreateWorkItem(101, parentTfsId: 10, storyPoints: null, businessValue: null);
        var siblings = new[]
        {
            new StoryPointResolutionCandidate(CreateWorkItem(102, parentTfsId: 10, storyPoints: 3), false),
            new StoryPointResolutionCandidate(CreateWorkItem(103, parentTfsId: 10, storyPoints: null, businessValue: 7), false),
            new StoryPointResolutionCandidate(CreateWorkItem(104, parentTfsId: 99, storyPoints: 100), false),
            new StoryPointResolutionCandidate(CreateWorkItem(105, type: WorkItemType.Bug, parentTfsId: 10, storyPoints: 50), false)
        };

        var result = _service.Resolve(new StoryPointResolutionRequest(
            target,
            IsDone: false,
            FeaturePbis: siblings));

        Assert.AreEqual(5d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Derived, result.Source);
    }

    [TestMethod]
    public void Resolve_KeepsDerivedEstimateFractional()
    {
        var target = CreateWorkItem(101, parentTfsId: 10, storyPoints: null, businessValue: null);
        var siblings = new[]
        {
            new StoryPointResolutionCandidate(CreateWorkItem(102, parentTfsId: 10, storyPoints: 3), false),
            new StoryPointResolutionCandidate(CreateWorkItem(103, parentTfsId: 10, storyPoints: 4), false)
        };

        var result = _service.Resolve(new StoryPointResolutionRequest(
            target,
            IsDone: false,
            FeaturePbis: siblings));

        Assert.AreEqual(3.5d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Derived, result.Source);
    }

    [TestMethod]
    public void Resolve_ReturnsExpectedEstimateSourceClassification()
    {
        var real = _service.Resolve(new StoryPointResolutionRequest(CreateWorkItem(101, storyPoints: 5), IsDone: false));
        var fallback = _service.Resolve(new StoryPointResolutionRequest(CreateWorkItem(102, storyPoints: null, businessValue: 8), IsDone: false));
        var missing = _service.Resolve(new StoryPointResolutionRequest(CreateWorkItem(103, storyPoints: null, businessValue: null), IsDone: false));
        var derived = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(104, parentTfsId: 10, storyPoints: null, businessValue: null),
            IsDone: false,
            [
                new StoryPointResolutionCandidate(CreateWorkItem(105, parentTfsId: 10, storyPoints: 2), false),
                new StoryPointResolutionCandidate(CreateWorkItem(106, parentTfsId: 10, storyPoints: 6), false)
            ]));

        Assert.AreEqual(StoryPointEstimateSource.Real, real.Source);
        Assert.AreEqual(StoryPointEstimateSource.Fallback, fallback.Source);
        Assert.AreEqual(StoryPointEstimateSource.Missing, missing.Source);
        Assert.AreEqual(StoryPointEstimateSource.Derived, derived.Source);
    }

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type = WorkItemType.Pbi,
        int? parentTfsId = null,
        int? storyPoints = null,
        int? businessValue = null)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: $"Work Item {tfsId}",
            ParentTfsId: parentTfsId,
            AreaPath: "\\Project",
            IterationPath: "\\Project\\Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            BusinessValue: businessValue,
            BacklogPriority: null,
            StoryPoints: storyPoints);
    }
}
