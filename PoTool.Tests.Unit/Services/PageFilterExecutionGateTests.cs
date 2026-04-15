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
            false,
            false,
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
        CollectionAssert.AreEquivalent(
            new[] { "teamids", "time" },
            result.BlockingFields.ToArray());
    }

    [TestMethod]
    public void Evaluate_NotAppliedDimensions_AreReportedWithoutBlockingQueries()
    {
        var result = _gate.Evaluate(new FilterStateResolution(
            "HealthWorkspace",
            "home/health",
            "home/health",
            false,
            false,
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

    [TestMethod]
    public void Evaluate_InvalidProductIssue_MapsBlockingField()
    {
        var result = _gate.Evaluate(new FilterStateResolution(
            "PortfolioDelivery",
            "home/delivery/portfolio",
            "home/delivery/portfolio",
            false,
            false,
            UsesProduct: true,
            UsesProject: false,
            UsesTeam: true,
            UsesTime: true,
            new FilterState([99], Array.Empty<string>(), 7, new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: 100, EndSprintId: 101)),
            FilterResolutionStatus.Invalid,
            MissingTeam: false,
            MissingSprint: false,
            ActiveProfileId: 1,
            FilterUpdateSource.Ui,
            Array.Empty<string>(),
            ["Selected global product '99' is not available in the current route scope."],
            DateTimeOffset.UtcNow));

        Assert.IsFalse(result.CanExecuteQueries);
        CollectionAssert.AreEqual(new[] { "productids" }, result.BlockingFields.ToArray());
    }
}
