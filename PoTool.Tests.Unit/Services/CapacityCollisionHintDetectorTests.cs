using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CapacityCollisionHintDetectorTests
{
    private static readonly DateTimeOffset AxisStart = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AxisEnd = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Build_ReturnsCollisionSegments_WhenOverlappingEpicsShareATeam()
    {
        var collisions = CapacityCollisionHintDetector.Build(
            [
                new CapacityCollisionEpicInput(1, "Platform", [7], new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero)),
                new CapacityCollisionEpicInput(2, "Mobile", [7, 9], new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 12, 0, 0, 0, TimeSpan.Zero))
            ],
            AxisStart,
            AxisEnd);

        Assert.HasCount(1, collisions);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero), collisions[0].StartDate);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero), collisions[0].EndDate);
        CollectionAssert.AreEqual(new[] { 1, 2 }, collisions[0].EpicIds.ToArray());
        CollectionAssert.AreEqual(new[] { 7 }, collisions[0].SharedTeamIds.ToArray());
    }

    [TestMethod]
    public void Build_IgnoresOverlaps_WhenTeamsDoNotIntersect()
    {
        var collisions = CapacityCollisionHintDetector.Build(
            [
                new CapacityCollisionEpicInput(1, "Platform", [7], new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero)),
                new CapacityCollisionEpicInput(2, "Mobile", [8], new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 12, 0, 0, 0, TimeSpan.Zero))
            ],
            AxisStart,
            AxisEnd);

        Assert.IsEmpty(collisions);
    }

    [TestMethod]
    public void Build_IgnoresProductsWithoutTeams()
    {
        var collisions = CapacityCollisionHintDetector.Build(
            [
                new CapacityCollisionEpicInput(1, "Platform", [], new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero)),
                new CapacityCollisionEpicInput(2, "Mobile", [7], new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 12, 0, 0, 0, TimeSpan.Zero))
            ],
            AxisStart,
            AxisEnd);

        Assert.IsEmpty(collisions);
    }
}
