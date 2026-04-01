using PoTool.Client.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class SprintCadenceResolverTests
{
    [TestMethod]
    public void Resolve_UsesAverageOfRecentCompletedSprints()
    {
        var now = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintDto>
        {
            new(1, 10, null, "A", "Sprint 1", now.AddDays(-50), now.AddDays(-40), "past", now),
            new(2, 10, null, "B", "Sprint 2", now.AddDays(-39), now.AddDays(-28), "past", now),
            new(3, 10, null, "C", "Sprint 3", now.AddDays(-27), now.AddDays(-13), "past", now),
            new(4, 10, null, "D", "Sprint 4", now.AddDays(-12), now.AddDays(-1), "past", now),
            new(5, 10, null, "E", "Sprint 5", now.AddDays(-80), now.AddDays(-70), "past", now),
            new(6, 10, null, "F", "Sprint 6", now.AddDays(-95), now.AddDays(-85), "past", now)
        };

        var cadence = SprintCadenceResolver.Resolve(sprints, now);

        Assert.AreEqual(SprintCadenceSource.CompletedSprintAverage, cadence.Source);
        Assert.AreEqual(5, cadence.SampleCount);
        Assert.AreEqual(11.2d, cadence.DurationDays, 0.001d);
        Assert.IsFalse(cadence.UsesFallback);
    }

    [TestMethod]
    public void Resolve_UsesCurrentSprintWhenCompletedHistoryMissing()
    {
        var now = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintDto>
        {
            new(1, 10, null, "Current", "Current", now.AddDays(-3), now.AddDays(11), "current", now)
        };

        var cadence = SprintCadenceResolver.Resolve(sprints, now);

        Assert.AreEqual(SprintCadenceSource.CurrentSprintFallback, cadence.Source);
        Assert.AreEqual(14d, cadence.DurationDays, 0.001d);
        Assert.IsTrue(cadence.UsesFallback);
        Assert.IsFalse(cadence.UsesDefaultDuration);
    }

    [TestMethod]
    public void Resolve_UsesDefaultWhenNoValidSprintDatesExist()
    {
        var cadence = SprintCadenceResolver.Resolve(
            [
                new SprintDto(1, 10, null, "Invalid", "Invalid", null, null, null, DateTimeOffset.UtcNow)
            ]);

        Assert.AreEqual(SprintCadenceSource.DefaultFallback, cadence.Source);
        Assert.AreEqual(SprintCadenceResolver.DefaultFallbackSprintDurationDays, cadence.DurationDays, 0.001d);
        Assert.IsTrue(cadence.UsesDefaultDuration);
    }
}
