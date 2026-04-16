using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class GlobalFilterTrendRequestMapperTests
{
    [TestMethod]
    public void ResolveRange_BuildsInclusiveSprintIdsAndBoundaries()
    {
        var state = new FilterState(
            Array.Empty<int>(),
            Array.Empty<string>(),
            10,
            new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: 45, EndSprintId: 49));
        var sprints = new[]
        {
            CreateSprint(45, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint(46, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint(47, new DateTimeOffset(2026, 1, 29, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint(48, new DateTimeOffset(2026, 2, 12, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 25, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint(49, new DateTimeOffset(2026, 2, 26, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 11, 0, 0, 0, TimeSpan.Zero))
        };

        var result = GlobalFilterTrendRequestMapper.ResolveRange(state, sprints);

        Assert.IsTrue(result.IsResolved);
        CollectionAssert.AreEqual(new[] { 45, 46, 47, 48, 49 }, result.SprintIds.ToArray());
        Assert.AreEqual(sprints[0].StartUtc, result.RangeStartUtc);
        Assert.AreEqual(sprints[^1].EndUtc, result.RangeEndUtc);
    }

    [TestMethod]
    public void ResolveRange_WhenBoundarySprintIsMissing_ReturnsUnresolved()
    {
        var state = new FilterState(
            Array.Empty<int>(),
            Array.Empty<string>(),
            10,
            new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: 45, EndSprintId: 49));
        var sprints = new[]
        {
            CreateSprint(45, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint(46, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 28, 0, 0, 0, TimeSpan.Zero))
        };

        var result = GlobalFilterTrendRequestMapper.ResolveRange(state, sprints);

        Assert.IsFalse(result.IsResolved);
        CollectionAssert.AreEqual(Array.Empty<int>(), result.SprintIds.ToArray());
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    private static SprintDto CreateSprint(int id, DateTimeOffset startUtc, DateTimeOffset endUtc)
        => new()
        {
            Id = id,
            TeamId = 10,
            Name = $"Sprint {id}",
            StartUtc = startUtc,
            EndUtc = endUtc
        };
}
