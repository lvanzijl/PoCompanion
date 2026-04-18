using PoTool.Core.Domain.Planning;

namespace PoTool.Core.Domain.Tests;

[TestClass]
public sealed class PlanningOperationServiceTests
{
    private readonly PlanningOperationService _operationService = new();
    private readonly PlanningRecomputeService _recomputeService = new();
    private readonly PlanningValidationService _validationService = new();

    [TestMethod]
    public void RecomputeFrom_UsesPlannedRoadmapAndTrackFloors()
    {
        var state = new PlanningState(
        [
            new PlanningEpicState(101, 1, 2, 0, 3, 0),
            new PlanningEpicState(102, 2, 1, 0, 2, 1),
            new PlanningEpicState(103, 3, 0, 0, 2, 1),
        ]);

        var recomputed = _recomputeService.RecomputeFrom(state, 0);

        Assert.AreEqual(2, recomputed.Epics[0].ComputedStartSprintIndex);
        Assert.AreEqual(2, recomputed.Epics[1].ComputedStartSprintIndex);
        Assert.AreEqual(4, recomputed.Epics[2].ComputedStartSprintIndex);
    }

    [TestMethod]
    public void RecomputeFrom_PreservesEarlierPrefixWhenRecomputingSuffix()
    {
        var initial = _recomputeService.RecomputeFrom(
            new PlanningState(
            [
                new PlanningEpicState(101, 1, 0, 0, 2, 0),
                new PlanningEpicState(102, 2, 0, 0, 2, 1),
                new PlanningEpicState(103, 3, 3, 0, 1, 1),
            ]),
            0);

        var updatedSuffixState = new PlanningState(
        [
            initial.Epics[0],
            initial.Epics[1],
            initial.Epics[2] with { PlannedStartSprintIndex = 5 },
        ]);

        var recomputed = _recomputeService.RecomputeFrom(updatedSuffixState, 2);

        Assert.AreEqual(initial.Epics[0], recomputed.Epics[0]);
        Assert.AreEqual(initial.Epics[1], recomputed.Epics[1]);
        Assert.AreEqual(5, recomputed.Epics[2].ComputedStartSprintIndex);
    }

    [TestMethod]
    public void RecomputeFrom_MainLaneStaysSequentialBecauseTrackCannotOverlap()
    {
        var state = new PlanningState(
        [
            new PlanningEpicState(101, 1, 0, 0, 2, 0),
            new PlanningEpicState(102, 2, 0, 0, 1, 0),
            new PlanningEpicState(103, 3, 1, 0, 1, 0),
        ]);

        var recomputed = _recomputeService.RecomputeFrom(state, 0);

        CollectionAssert.AreEqual(
            new[] { 0, 2, 3 },
            recomputed.Epics.Select(epic => epic.ComputedStartSprintIndex).ToArray());
    }

    [TestMethod]
    public void RunInParallel_AllowsCrossTrackOverlapAndReusesLowestTrack()
    {
        var state = _recomputeService.RecomputeFrom(
            new PlanningState(
            [
                new PlanningEpicState(101, 1, 0, 0, 4, 0),
                new PlanningEpicState(102, 2, 0, 0, 1, 1),
                new PlanningEpicState(103, 3, 1, 0, 2, 0),
            ]),
            0);

        var result = _operationService.RunInParallel(state, 103);

        Assert.IsEmpty(result.ValidationIssues);
        Assert.AreEqual(1, result.State.Epics[2].TrackIndex);
        Assert.AreEqual(1, result.State.Epics[2].ComputedStartSprintIndex);
        CollectionAssert.AreEquivalent(new[] { 103 }, result.ChangedEpicIds.ToArray());
        CollectionAssert.AreEqual(new[] { 103 }, result.AffectedEpicIds.ToArray());
    }

    [TestMethod]
    public void ReturnToMain_DelaysEpicUntilMainLaneIsAvailable()
    {
        var state = _recomputeService.RecomputeFrom(
            new PlanningState(
            [
                new PlanningEpicState(101, 1, 0, 0, 4, 0),
                new PlanningEpicState(102, 2, 0, 0, 1, 1),
                new PlanningEpicState(103, 3, 1, 0, 2, 1),
            ]),
            0);

        var result = _operationService.ReturnToMain(state, 103);

        Assert.IsEmpty(result.ValidationIssues);
        Assert.AreEqual(0, result.State.Epics[2].TrackIndex);
        Assert.AreEqual(4, result.State.Epics[2].ComputedStartSprintIndex);
        CollectionAssert.AreEqual(new[] { 103 }, result.ChangedEpicIds.ToArray());
    }

    [TestMethod]
    public void Validation_RejectsInvalidInputsAndConstraintViolations()
    {
        var issues = _validationService.Validate(
            new PlanningState(
            [
                new PlanningEpicState(101, 1, -1, 3, 0, -1),
                new PlanningEpicState(101, 3, 2, 2, 1, 0),
                new PlanningEpicState(103, 4, 2, 2, 1, 0),
            ]));

        CollectionAssert.IsSubsetOf(
            new[]
            {
                PlanningValidationIssueCode.DuplicateEpicId,
                PlanningValidationIssueCode.InvalidDuration,
                PlanningValidationIssueCode.NegativePlannedStart,
                PlanningValidationIssueCode.InvalidTrackIndex,
                PlanningValidationIssueCode.InvalidRoadmapOrder,
                PlanningValidationIssueCode.RoadmapStartOrderViolation,
                PlanningValidationIssueCode.SameTrackOverlap,
            },
            issues.Select(issue => issue.Code).Distinct().ToArray());
    }

    [TestMethod]
    public void MoveEpicBySprints_RejectsUnknownEpic()
    {
        var state = _recomputeService.RecomputeFrom(
            new PlanningState(
            [
                new PlanningEpicState(101, 1, 0, 0, 1, 0),
            ]),
            0);

        var result = _operationService.MoveEpicBySprints(state, 999, 1);

        Assert.HasCount(1, result.ValidationIssues);
        Assert.AreEqual(PlanningValidationIssueCode.EpicNotFound, result.ValidationIssues[0].Code);
        Assert.IsEmpty(result.ChangedEpicIds);
        Assert.IsEmpty(result.AffectedEpicIds);
    }
}
