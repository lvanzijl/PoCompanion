using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PageFilterExecutionGateTests
{
    private readonly PageFilterExecutionGate _gate = new();

    [TestMethod]
    public void Evaluate_UnresolvedState_BlocksQueriesAndExplainsMissingInputs()
    {
        var result = _gate.Evaluate(new FilterStateResolution(
            "SprintTrend",
            "home/delivery/sprint",
            "home/delivery/sprint",
            UsesProduct: true,
            UsesProject: false,
            UsesTeam: true,
            UsesTime: true,
            new FilterState(Array.Empty<int>(), Array.Empty<string>(), null, new FilterTimeSelection(FilterTimeMode.Sprint)),
            FilterResolutionStatus.Unresolved,
            MissingTeam: true,
            MissingSprint: true,
            ActiveProfileId: 1,
            FilterUpdateSource.Query,
            Array.Empty<string>(),
            Array.Empty<string>(),
            DateTimeOffset.UtcNow));

        Assert.IsFalse(result.CanExecuteQueries);
        Assert.HasCount(2, result.BlockingMessages);
    }

    [TestMethod]
    public void Evaluate_NotAppliedDimensions_AreReportedWithoutBlockingQueries()
    {
        var result = _gate.Evaluate(new FilterStateResolution(
            "HealthWorkspace",
            "home/health",
            "home/health",
            UsesProduct: false,
            UsesProject: false,
            UsesTeam: false,
            UsesTime: false,
            new FilterState([5], ["project-payments"], 7, new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: 100, EndSprintId: 101)),
            FilterResolutionStatus.Resolved,
            MissingTeam: false,
            MissingSprint: false,
            ActiveProfileId: 1,
            FilterUpdateSource.Ui,
            Array.Empty<string>(),
            Array.Empty<string>(),
            DateTimeOffset.UtcNow));

        Assert.IsTrue(result.CanExecuteQueries);
        Assert.HasCount(4, result.NotAppliedMessages);
    }
}
