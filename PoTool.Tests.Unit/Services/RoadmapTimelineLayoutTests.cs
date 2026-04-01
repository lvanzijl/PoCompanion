using PoTool.Client.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class RoadmapTimelineLayoutTests
{
    [TestMethod]
    public void Build_UsesIndependentAxisAcrossEpics()
    {
        var timeline = RoadmapTimelineLayout.Build(
            [
                new RoadmapTimelineEpicInput(1, "Epic A", 1, new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero), 2, ForecastConfidence.High, true, false),
                new RoadmapTimelineEpicInput(2, "Epic B", 2, new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero), 1, ForecastConfidence.High, true, false)
            ]);

        Assert.IsNotNull(timeline.AxisStart);
        Assert.IsNotNull(timeline.AxisEnd);
        Assert.AreEqual(new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero), timeline.AxisStart.Value);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero), timeline.AxisEnd.Value);
        Assert.AreEqual(0d, timeline.Rows[0].LeftPercent.GetValueOrDefault(), 0.001d);
        Assert.AreEqual(0d, timeline.Rows[1].LeftPercent.GetValueOrDefault(), 0.001d, "Earlier forecasts should start independently, not after previous roadmap rows.");
    }

    [TestMethod]
    public void Build_UsesFallbackWindowWhenSprintsRemainingMissing()
    {
        var timeline = RoadmapTimelineLayout.Build(
            [
                new RoadmapTimelineEpicInput(1, "Epic A", 1, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), null, ForecastConfidence.High, true, false)
            ]);

        Assert.AreEqual(new DateTimeOffset(2026, 4, 3, 0, 0, 0, TimeSpan.Zero), timeline.Rows[0].StartDate);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), timeline.Rows[0].EndDate);
    }

    [TestMethod]
    public void Build_FlagsLowConfidenceAndMissingForecastRows()
    {
        var timeline = RoadmapTimelineLayout.Build(
            [
                new RoadmapTimelineEpicInput(1, "Low Confidence", 1, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), 1, ForecastConfidence.Low, true, true),
                new RoadmapTimelineEpicInput(2, "Missing", 2, null, null, null, false, false)
            ]);

        Assert.IsTrue(timeline.Rows[0].HasTimelineBar);
        Assert.IsTrue(timeline.Rows[0].IsLowConfidence);
        Assert.IsTrue(timeline.Rows[0].IsDelayed);
        Assert.IsFalse(timeline.Rows[0].IsForecastMissing);

        Assert.IsFalse(timeline.Rows[1].HasTimelineBar);
        Assert.IsTrue(timeline.Rows[1].IsForecastMissing);
    }
}
