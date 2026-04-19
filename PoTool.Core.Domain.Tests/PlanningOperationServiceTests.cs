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

    [TestMethod]
    public void ReorderEpic_MovingEarlier_RenumbersContiguouslyAndPreservesTrackAndDuration()
    {
        var state = Recompute(
            new PlanningEpicState(101, 1, 0, 0, 2, 0),
            new PlanningEpicState(102, 2, 3, 0, 2, 1),
            new PlanningEpicState(103, 3, 1, 0, 1, 1));

        var result = _operationService.ReorderEpic(state, 103, 2);

        Assert.IsEmpty(result.ValidationIssues);
        CollectionAssert.AreEqual(new[] { 101, 103, 102 }, result.State.Epics.Select(epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.State.Epics.Select(epic => epic.RoadmapOrder).ToArray());
        Assert.AreEqual(1, result.State.Epics[1].ComputedStartSprintIndex);
        Assert.AreEqual(1, result.State.Epics[1].TrackIndex);
        Assert.AreEqual(1, result.State.Epics[1].DurationInSprints);
        Assert.AreEqual(3, result.State.Epics[2].ComputedStartSprintIndex);
        CollectionAssert.AreEquivalent(new[] { 103, 102 }, result.ChangedEpicIds.ToArray());
        CollectionAssert.AreEqual(new[] { 103, 102 }, result.AffectedEpicIds.ToArray());
    }

    [TestMethod]
    public void ReorderEpic_MovingLater_RecomputesFromEarliestAffectedIndex()
    {
        var state = Recompute(
            new PlanningEpicState(101, 1, 0, 0, 2, 0),
            new PlanningEpicState(102, 2, 0, 0, 1, 1),
            new PlanningEpicState(103, 3, 1, 0, 1, 0));

        var result = _operationService.ReorderEpic(state, 101, 3);

        Assert.IsEmpty(result.ValidationIssues);
        CollectionAssert.AreEqual(new[] { 102, 103, 101 }, result.State.Epics.Select(epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.State.Epics.Select(epic => epic.ComputedStartSprintIndex).ToArray());
        CollectionAssert.AreEquivalent(new[] { 101, 102, 103 }, result.ChangedEpicIds.ToArray());
        CollectionAssert.AreEqual(new[] { 102, 103, 101 }, result.AffectedEpicIds.ToArray());
    }

    [TestMethod]
    public void ReorderEpic_ParallelEpicStillRespectsNewRoadmapPredecessorAndSameTrackRules()
    {
        var state = Recompute(
            new PlanningEpicState(101, 1, 4, 0, 4, 0),
            new PlanningEpicState(102, 2, 0, 0, 2, 1),
            new PlanningEpicState(103, 3, 0, 0, 1, 1));

        var result = _operationService.ReorderEpic(state, 103, 2);

        Assert.IsEmpty(result.ValidationIssues);
        Assert.AreEqual(4, result.State.Epics[1].ComputedStartSprintIndex);
        Assert.AreEqual(5, result.State.Epics[2].ComputedStartSprintIndex);
        Assert.IsGreaterThanOrEqualTo(result.State.Epics[0].ComputedStartSprintIndex, result.State.Epics[1].ComputedStartSprintIndex);
        Assert.IsGreaterThanOrEqualTo(result.State.Epics[1].EndSprintIndexExclusive, result.State.Epics[2].ComputedStartSprintIndex);
    }

    [TestMethod]
    public void ShiftPlan_ShiftsSuffixPreservesEarlierEpicsTracksAndRelativeRequestedShape()
    {
        var state = Recompute(
            new PlanningEpicState(101, 1, 0, 0, 2, 0),
            new PlanningEpicState(102, 2, 1, 0, 2, 1),
            new PlanningEpicState(103, 3, 3, 0, 1, 1),
            new PlanningEpicState(104, 4, 2, 0, 1, 0));

        var result = _operationService.ShiftPlan(state, 102, 4);

        Assert.IsEmpty(result.ValidationIssues);
        Assert.AreEqual(state.Epics[0], result.State.Epics[0]);
        CollectionAssert.AreEqual(new[] { 5, 7, 6 }, result.State.Epics.Skip(1).Select(epic => epic.PlannedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 1, 0 }, result.State.Epics.Skip(1).Select(epic => epic.TrackIndex).ToArray());
        Assert.AreEqual(2, result.State.Epics[2].PlannedStartSprintIndex - result.State.Epics[1].PlannedStartSprintIndex);
        Assert.AreEqual(1, result.State.Epics[3].PlannedStartSprintIndex - result.State.Epics[1].PlannedStartSprintIndex);
        CollectionAssert.AreEqual(new[] { 102, 103, 104 }, result.AffectedEpicIds.ToArray());
        CollectionAssert.AreEqual(new[] { 5, 7, 7 }, result.State.Epics.Skip(1).Select(epic => epic.ComputedStartSprintIndex).ToArray());
    }

    [TestMethod]
    public void ReorderEpic_RejectsUnknownEpicAndInvalidTargetOrder()
    {
        var state = Recompute(
            new PlanningEpicState(101, 1, 0, 0, 1, 0),
            new PlanningEpicState(102, 2, 1, 0, 1, 0));

        var unknownEpicResult = _operationService.ReorderEpic(state, 999, 1);
        var invalidTargetResult = _operationService.ReorderEpic(state, 101, 0);

        Assert.HasCount(1, unknownEpicResult.ValidationIssues);
        Assert.AreEqual(PlanningValidationIssueCode.EpicNotFound, unknownEpicResult.ValidationIssues[0].Code);
        Assert.HasCount(1, invalidTargetResult.ValidationIssues);
        Assert.AreEqual(PlanningValidationIssueCode.InvalidOperationInput, invalidTargetResult.ValidationIssues[0].Code);
    }

    [TestMethod]
    public void ShiftPlan_RejectsUnknownEpicAndNonPositiveDelta()
    {
        var state = Recompute(
            new PlanningEpicState(101, 1, 0, 0, 1, 0),
            new PlanningEpicState(102, 2, 1, 0, 1, 0));

        var unknownEpicResult = _operationService.ShiftPlan(state, 999, 2);
        var invalidDeltaResult = _operationService.ShiftPlan(state, 101, 0);

        Assert.HasCount(1, unknownEpicResult.ValidationIssues);
        Assert.AreEqual(PlanningValidationIssueCode.EpicNotFound, unknownEpicResult.ValidationIssues[0].Code);
        Assert.HasCount(1, invalidDeltaResult.ValidationIssues);
        Assert.AreEqual(PlanningValidationIssueCode.InvalidOperationInput, invalidDeltaResult.ValidationIssues[0].Code);
    }

    [TestMethod]
    public void MixedScenario_Slice1AndSlice2OperationsPreserveFinalInvariants()
    {
        var initialState = Recompute(
            new PlanningEpicState(101, 1, 0, 0, 3, 0),
            new PlanningEpicState(102, 2, 0, 0, 2, 0),
            new PlanningEpicState(103, 3, 1, 0, 2, 0),
            new PlanningEpicState(104, 4, 1, 0, 1, 0));

        var movedState = _operationService.MoveEpicBySprints(initialState, 103, -1).State;
        var parallelState = _operationService.RunInParallel(movedState, 103).State;
        var reorderedState = _operationService.ReorderEpic(parallelState, 104, 2).State;
        var finalResult = _operationService.ShiftPlan(reorderedState, 102, 2);

        Assert.IsEmpty(finalResult.ValidationIssues);
        CollectionAssert.AreEqual(new[] { 101, 104, 102, 103 }, finalResult.State.Epics.Select(epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, finalResult.State.Epics.Select(epic => epic.RoadmapOrder).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 1 }, finalResult.State.Epics.Select(epic => epic.TrackIndex).ToArray());
        Assert.IsEmpty(_validationService.Validate(finalResult.State));
        Assert.IsGreaterThanOrEqualTo(finalResult.State.Epics[0].ComputedStartSprintIndex, finalResult.State.Epics[1].ComputedStartSprintIndex);
        Assert.IsGreaterThanOrEqualTo(finalResult.State.Epics[1].EndSprintIndexExclusive, finalResult.State.Epics[2].ComputedStartSprintIndex);
    }

    private PlanningState Recompute(params PlanningEpicState[] epics)
    {
        return _recomputeService.RecomputeFrom(new PlanningState(epics), 0);
    }
}
