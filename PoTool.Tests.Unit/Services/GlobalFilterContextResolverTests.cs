using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GlobalFilterContextResolverTests
{
    [TestMethod]
    public void GetAvailableTeams_ProductScopeFiltersTeamOptions()
    {
        var resolver = new GlobalFilterContextResolver();
        var products = new[]
        {
            CreateProduct(11, [7]),
            CreateProduct(12, [8])
        };
        var teams = new[]
        {
            CreateTeam(7, "Alpha"),
            CreateTeam(8, "Bravo")
        };

        var availableTeams = resolver.GetAvailableTeams(11, products, teams);

        CollectionAssert.AreEqual(new[] { 7 }, availableTeams.Select(team => team.Id).ToArray());
    }

    [TestMethod]
    public void NormalizeState_RemovesInvalidTeamAndSprintSelections()
    {
        var resolver = new GlobalFilterContextResolver();
        var products = new[]
        {
            CreateProduct(11, [7])
        };
        var currentState = new FilterState([11], Array.Empty<string>(), 8, new FilterTimeSelection(FilterTimeMode.Sprint, SprintId: 701));
        var proposedState = new FilterLocalBridgeState(
            ProductId: 11,
            TeamId: 8,
            SprintId: 701,
            FromSprintId: 700,
            ToSprintId: 701);

        var normalized = resolver.NormalizeState(currentState, proposedState, products);

        Assert.IsNull(normalized.TeamId);
        Assert.IsNull(normalized.SprintId);
        Assert.IsNull(normalized.FromSprintId);
        Assert.IsNull(normalized.ToSprintId);
    }

    private static ProductDto CreateProduct(int id, List<int> teamIds)
        => new(id, 42, $"Product {id}", [], 0, ProductPictureType.Default, 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, teamIds, []);

    private static TeamDto CreateTeam(int id, string name)
        => new(id, name, $"\\Project\\{name}", false, TeamPictureType.Default, 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null, null);
}
