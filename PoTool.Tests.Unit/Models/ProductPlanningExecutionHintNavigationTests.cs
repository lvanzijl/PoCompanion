using PoTool.Client.Models;
using PoTool.Shared.Planning;

namespace PoTool.Tests.Unit.Models;

[TestClass]
public sealed class ProductPlanningExecutionHintNavigationTests
{
    [TestMethod]
    public void BuildTargetState_ForSprintExecution_UsesHintTeamAndSprint()
    {
        var hint = new ProductPlanningExecutionHintDto(
            "spillover-increase",
            "Execution signal: direct spillover increasing",
            "Open Sprint Execution to inspect which committed work carried into the next sprint.",
            10,
            22);
        var currentState = new FilterState(
            [7],
            ["project-1"],
            TeamId: 99,
            new FilterTimeSelection(FilterTimeMode.Snapshot));

        var state = ProductPlanningExecutionHintNavigation.BuildTargetState(hint, currentState, productId: 7);

        Assert.AreEqual(WorkspaceRoutes.SprintExecution, ProductPlanningExecutionHintNavigation.ResolveRoute(hint));
        CollectionAssert.AreEqual(new[] { 7 }, state.ProductIds.ToArray());
        CollectionAssert.AreEqual(new[] { "project-1" }, state.ProjectIds.ToArray());
        Assert.AreEqual(10, state.TeamId);
        Assert.AreEqual(FilterTimeMode.Sprint, state.Time.Mode);
        Assert.AreEqual(22, state.Time.SprintId);
    }

    [TestMethod]
    public void BuildTargetState_ForDeliveryTrends_UsesExistingResolvedRange()
    {
        var hint = new ProductPlanningExecutionHintDto(
            "completion-variability",
            "Execution signal: delivery consistency outside typical range",
            "Open Delivery Trends to inspect when this instability started across recent sprints.",
            10,
            22);
        var currentState = new FilterState(
            [7],
            ["project-1"],
            TeamId: 10,
            new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: 14, EndSprintId: 22));

        var state = ProductPlanningExecutionHintNavigation.BuildTargetState(hint, currentState, productId: 7);

        Assert.AreEqual(WorkspaceRoutes.DeliveryTrends, ProductPlanningExecutionHintNavigation.ResolveRoute(hint));
        Assert.AreEqual(FilterTimeMode.Range, state.Time.Mode);
        Assert.AreEqual(14, state.Time.StartSprintId);
        Assert.AreEqual(22, state.Time.EndSprintId);
    }

    [TestMethod]
    public void BuildTargetState_ForDeliveryTrends_FallsBackToSingleSprintRange_WhenCurrentRangeIsUnavailable()
    {
        var hint = new ProductPlanningExecutionHintDto(
            "completion-variability",
            "Execution signal: delivery consistency outside typical range",
            "Open Delivery Trends to inspect when this instability started across recent sprints.",
            10,
            22);
        var currentState = new FilterState(
            [7],
            ["project-1"],
            TeamId: null,
            new FilterTimeSelection(FilterTimeMode.Snapshot));

        var state = ProductPlanningExecutionHintNavigation.BuildTargetState(hint, currentState, productId: 7);

        Assert.AreEqual(FilterTimeMode.Range, state.Time.Mode);
        Assert.AreEqual(22, state.Time.StartSprintId);
        Assert.AreEqual(22, state.Time.EndSprintId);
    }
}
