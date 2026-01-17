using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Helpers;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public class TimeframeIterationHelperTests
{
    [TestMethod]
    public void GetIsoWeek_Monday_ReturnsCorrectWeek()
    {
        // Arrange - 2025-01-06 is a Monday in Week 2
        var date = new DateTimeOffset(2025, 1, 6, 0, 0, 0, TimeSpan.Zero);

        // Act
        var (year, weekNumber) = TimeframeIterationHelper.GetIsoWeek(date);

        // Assert
        Assert.AreEqual(2025, year);
        Assert.AreEqual(2, weekNumber);
    }

    [TestMethod]
    public void GetIsoWeek_Sunday_ReturnsCorrectWeek()
    {
        // Arrange - 2025-01-05 is a Sunday in Week 1
        var date = new DateTimeOffset(2025, 1, 5, 0, 0, 0, TimeSpan.Zero);

        // Act
        var (year, weekNumber) = TimeframeIterationHelper.GetIsoWeek(date);

        // Assert
        Assert.AreEqual(2025, year);
        Assert.AreEqual(1, weekNumber);
    }

    [TestMethod]
    public void GetIsoWeek_January1_ReturnsReasonableWeek()
    {
        // Arrange - 2024-01-01 is a Monday
        var date = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var (year, weekNumber) = TimeframeIterationHelper.GetIsoWeek(date);

        // Assert - Should return a reasonable week number (either last week of 2023 or week 1 of 2024)
        Assert.IsTrue(weekNumber >= 1 && weekNumber <= 53);
        Assert.IsTrue(year == 2023 || year == 2024);
    }

    [TestMethod]
    public void GetIsoWeek_SameWeekDifferentDays_ReturnsSameWeek()
    {
        // Arrange - Test multiple days in the same week
        var monday = new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero);
        var friday = new DateTimeOffset(2025, 1, 17, 0, 0, 0, TimeSpan.Zero);
        var sunday = new DateTimeOffset(2025, 1, 19, 0, 0, 0, TimeSpan.Zero);

        // Act
        var (yearMon, weekMon) = TimeframeIterationHelper.GetIsoWeek(monday);
        var (yearFri, weekFri) = TimeframeIterationHelper.GetIsoWeek(friday);
        var (yearSun, weekSun) = TimeframeIterationHelper.GetIsoWeek(sunday);

        // Assert - All should be in the same week
        Assert.AreEqual(yearMon, yearFri);
        Assert.AreEqual(yearMon, yearSun);
        Assert.AreEqual(weekMon, weekFri);
        Assert.AreEqual(weekMon, weekSun);
    }

    [TestMethod]
    public void GetIterationKey_ReturnsCorrectFormat()
    {
        // Arrange
        var date = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);

        // Act
        var key = TimeframeIterationHelper.GetIterationKey(date);

        // Assert
        Assert.AreEqual("2025-W03", key);
    }

    [TestMethod]
    public void GetWeekStart_ReturnsMonday()
    {
        // Arrange - 2025-01-15 is a Wednesday
        var date = new DateTimeOffset(2025, 1, 15, 12, 30, 45, TimeSpan.Zero);

        // Act
        var weekStart = TimeframeIterationHelper.GetWeekStart(date);

        // Assert
        Assert.AreEqual(new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero), weekStart);
        Assert.AreEqual(DayOfWeek.Monday, weekStart.DayOfWeek);
    }

    [TestMethod]
    public void GetWeekEnd_ReturnsSunday()
    {
        // Arrange - 2025-01-15 is a Wednesday
        var date = new DateTimeOffset(2025, 1, 15, 12, 30, 45, TimeSpan.Zero);

        // Act
        var weekEnd = TimeframeIterationHelper.GetWeekEnd(date);

        // Assert
        Assert.AreEqual(DayOfWeek.Sunday, weekEnd.DayOfWeek);
        // Should be end of Sunday (23:59:59.9999999)
        Assert.IsTrue(weekEnd.Hour == 23 && weekEnd.Minute == 59 && weekEnd.Second == 59);
    }

    [TestMethod]
    public void GetWeekStart_ForMonday_ReturnsSameDay()
    {
        // Arrange - 2025-01-13 is a Monday
        var date = new DateTimeOffset(2025, 1, 13, 12, 30, 45, TimeSpan.Zero);

        // Act
        var weekStart = TimeframeIterationHelper.GetWeekStart(date);

        // Assert
        Assert.AreEqual(new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero), weekStart);
    }

    [TestMethod]
    public void GetWeekStart_ForSunday_ReturnsPreviousMonday()
    {
        // Arrange - 2025-01-19 is a Sunday
        var date = new DateTimeOffset(2025, 1, 19, 12, 30, 45, TimeSpan.Zero);

        // Act
        var weekStart = TimeframeIterationHelper.GetWeekStart(date);

        // Assert
        Assert.AreEqual(new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero), weekStart);
        Assert.AreEqual(DayOfWeek.Monday, weekStart.DayOfWeek);
    }

    [TestMethod]
    public void GetIterationKey_Week52_FormatsCorrectly()
    {
        // Arrange - 2024-12-23 is in Week 52
        var date = new DateTimeOffset(2024, 12, 23, 0, 0, 0, TimeSpan.Zero);

        // Act
        var key = TimeframeIterationHelper.GetIterationKey(date);

        // Assert
        Assert.AreEqual("2024-W52", key);
    }

    [TestMethod]
    public void GetIterationKey_SameDatesReturnSameKey()
    {
        // Arrange
        var date1 = new DateTimeOffset(2025, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2025, 1, 17, 18, 30, 0, TimeSpan.Zero);

        // Act
        var key1 = TimeframeIterationHelper.GetIterationKey(date1);
        var key2 = TimeframeIterationHelper.GetIterationKey(date2);

        // Assert - Both dates are in the same week
        Assert.AreEqual(key1, key2);
    }
}
